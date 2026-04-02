using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Chatbot.Application.Services;

file static class ChatDefaults
{
    public const string DefaultTemplateId = "grounded_answer";
}

/// <summary>
/// Serviço de orquestração de chat
/// </summary>
public class ChatOrchestratorService : IChatOrchestrator
{
    private static readonly Regex SentenceBoundaryRegex = new(@"(?<=[\.!\?])\s+", RegexOptions.Compiled);
    private static readonly HashSet<string> EvidenceStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "o", "as", "os", "de", "do", "da", "dos", "das", "e", "em", "no", "na", "nos", "nas",
        "para", "por", "com", "sem", "sobre", "qual", "quais", "como", "que", "uma", "um", "ao", "aos",
        "se", "ou", "the", "and", "for", "with", "from", "this", "that"
    };

    private readonly IRetrievalService _retrievalService;
    private readonly ICitationAssembler _citationAssembler;
    private readonly IAgenticChatPlanner _agenticChatPlanner;
    private readonly IChatCompletionProvider _chatCompletionProvider;
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly IChatSessionStore _chatSessionStore;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly IApplicationCache _applicationCache;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<ChatOrchestratorService> _logger;

    private sealed class PreparedChatTurn
    {
        public string ResponseMessage { get; init; } = string.Empty;
        public List<CitationDto> Citations { get; init; } = new();
        public UsageMetadataDto Usage { get; init; } = new();
        public ChatPolicyDto Policy { get; init; } = new();
    }

    public ChatOrchestratorService(
        IRetrievalService retrievalService,
        ICitationAssembler citationAssembler,
        IAgenticChatPlanner agenticChatPlanner,
        IChatCompletionProvider chatCompletionProvider,
        IPromptTemplateRegistry promptTemplateRegistry,
        IPromptInjectionDetector promptInjectionDetector,
        IChatSessionStore chatSessionStore,
        IRequestContextAccessor requestContextAccessor,
        ISecurityAuditLogger securityAuditLogger,
        IApplicationCache applicationCache,
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        ResiliencePipeline resiliencePipeline,
        ILogger<ChatOrchestratorService> logger)
    {
        _retrievalService = retrievalService;
        _citationAssembler = citationAssembler;
        _agenticChatPlanner = agenticChatPlanner;
        _chatCompletionProvider = chatCompletionProvider;
        _promptTemplateRegistry = promptTemplateRegistry;
        _promptInjectionDetector = promptInjectionDetector;
        _chatSessionStore = chatSessionStore;
        _requestContextAccessor = requestContextAccessor;
        _securityAuditLogger = securityAuditLogger;
        _applicationCache = applicationCache;
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task<ChatResponseDto> SendAsync(ChatRequestDto request, CancellationToken ct)
    {
        _logger.LogInformation("Processing chat request for session {sessionId}", request.SessionId);

        var answerId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        var template = ResolveTemplate(request);

        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("chat.send");
            activity?.SetTag("chat.session_id", request.SessionId);
            activity?.SetTag("chat.template_id", request.TemplateId);
            var plan = _agenticChatPlanner.CreatePlan(request);
            activity?.SetTag("chat.execution_mode", plan.ExecutionMode);
            var preparedTurn = await PrepareTurnAsync(request, template, plan, startTime, ct);

            await PersistTurnAsync(request, answerId, template, preparedTurn, ct);

            return new ChatResponseDto
            {
                AnswerId = answerId,
                SessionId = request.SessionId,
                Message = preparedTurn.ResponseMessage,
                Citations = preparedTurn.Citations,
                Usage = preparedTurn.Usage,
                Policy = preparedTurn.Policy,
                TimestampUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            throw;
        }
    }

    public async IAsyncEnumerable<StreamingChatEventDto> StreamAsync(ChatRequestDto request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var answerId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        // Send started event
        yield return new StreamingChatEventDto
        {
            EventType = "started",
            Data = new StreamStartedEventDto { AnswerId = answerId, SessionId = request.SessionId }
        };

        var template = ResolveTemplate(request);
        var plan = _agenticChatPlanner.CreatePlan(request);
        using var activity = ChatbotTelemetry.ActivitySource.StartActivity("chat.stream");
        activity?.SetTag("chat.session_id", request.SessionId);
        activity?.SetTag("chat.execution_mode", plan.ExecutionMode);
        var preparedTurn = await PrepareTurnAsync(request, template, plan, startTime, ct);

        foreach (var segment in SegmentForStreaming(preparedTurn.ResponseMessage))
        {
            yield return new StreamingChatEventDto
            {
                EventType = "delta",
                Data = new StreamDeltaEventDto { Text = segment }
            };
        }

        // Send citations
        foreach (var citation in preparedTurn.Citations)
        {
            yield return new StreamingChatEventDto
            {
                EventType = "citation",
                Data = citation
            };
        }

        await PersistTurnAsync(request, answerId, template, preparedTurn, ct);

        // Send completed event
        yield return new StreamingChatEventDto
        {
            EventType = "completed",
            Data = new StreamCompletedEventDto
            {
                Usage = preparedTurn.Usage
            }
        };
    }

    private async Task<PreparedChatTurn> PrepareTurnAsync(
        ChatRequestDto request,
        PromptTemplateDefinition template,
        AgenticChatPlan plan,
        DateTime startTime,
        CancellationToken ct)
    {
        var maxContextChunks = Math.Min(_ragRuntimeSettings.MaxContextChunks, Math.Max(1, request.Options?.MaxCitations ?? 5));
        var shouldRetrieve = plan.RequiresRetrieval || HasExplicitRetrievalScope(request.Filters);
        var retrievalResult = shouldRetrieve
            ? await _resiliencePipeline.ExecuteAsync(async token => await _retrievalService.RetrieveAsync(new RetrievalQueryDto
            {
                Query = request.Message,
                TopK = maxContextChunks,
                DocumentIds = request.Filters?.DocumentIds,
                Tags = request.Filters?.Tags,
                Categories = request.Filters?.Categories,
                ContentTypes = request.Filters?.ContentTypes,
                Sources = request.Filters?.Sources,
                SemanticRanking = _featureFlagService.IsSemanticRankingEnabled && (request.Options?.SemanticRanking ?? true)
            }, token), ct)
            : new RetrievalResultDto
            {
                Chunks = new List<RetrievedChunkDto>(),
                RetrievalStrategy = "auto-llm",
                LatencyMs = 0
            };

        var evidenceChunks = SelectEvidenceChunks(request.Message, retrievalResult.Chunks, maxContextChunks);
        var citations = _citationAssembler.Assemble(evidenceChunks, request.Options?.MaxCitations ?? 5);
        var canUseGeneralKnowledge = plan.AllowsGeneralKnowledge;

        ChatCompletionResult? completionResult = null;
        var responseMessage = template.InsufficientEvidenceMessage;
        if (evidenceChunks.Count > 0 || canUseGeneralKnowledge)
        {
            var effectivePlan = new AgenticChatPlan
            {
                RequiresRetrieval = evidenceChunks.Count > 0,
                AllowsGeneralKnowledge = canUseGeneralKnowledge,
                PreferStreaming = plan.PreferStreaming,
                ExecutionMode = ResolveExecutionMode(evidenceChunks.Count > 0, canUseGeneralKnowledge)
            };
            var completionCacheKey = BuildChatCompletionCacheKey(request, template, effectivePlan, evidenceChunks);
            completionResult = await _applicationCache.GetAsync<ChatCompletionResult>(completionCacheKey, ct);

            if (completionResult is null)
            {
                completionResult = await _resiliencePipeline.ExecuteAsync(async token => await _chatCompletionProvider.CompleteAsync(new ChatCompletionRequest
                {
                    Message = request.Message,
                    Template = template,
                    AllowGeneralKnowledge = canUseGeneralKnowledge,
                    RetrievedChunks = evidenceChunks
                }, token), ct);

                if (!string.IsNullOrWhiteSpace(completionResult.Message))
                {
                    await _applicationCache.SetAsync(completionCacheKey, completionResult, _ragRuntimeSettings.ChatCompletionCacheTtl, ct);
                }
            }

            responseMessage = string.IsNullOrWhiteSpace(completionResult.Message)
                ? template.InsufficientEvidenceMessage
                : completionResult.Message;
        }

        var elapsed = DateTime.UtcNow - startTime;
        var usage = new UsageMetadataDto
        {
            Model = completionResult?.Model ?? "policy-only",
            PromptTokens = completionResult?.PromptTokens ?? 0,
            CompletionTokens = completionResult?.CompletionTokens ?? 0,
            TotalTokens = completionResult?.TotalTokens ?? 0,
            LatencyMs = (long)elapsed.TotalMilliseconds,
            RetrievalStrategy = ResolveRetrievalStrategy(shouldRetrieve, retrievalResult.RetrievalStrategy, evidenceChunks.Count, canUseGeneralKnowledge),
            RuntimeMetrics = new Dictionary<string, long>
            {
                ["max_context_chunks"] = maxContextChunks,
                ["retrieved_chunks"] = retrievalResult.Chunks.Count,
                ["evidence_chunks"] = evidenceChunks.Count,
                ["citations"] = citations.Count
            }
        };

        return new PreparedChatTurn
        {
            ResponseMessage = responseMessage,
            Citations = citations,
            Usage = usage,
            Policy = new ChatPolicyDto
            {
                Grounded = evidenceChunks.Count > 0,
                HadEnoughEvidence = evidenceChunks.Count > 0,
                TemplateId = template.TemplateId,
                TemplateVersion = template.Version
            }
        };
    }

    private async Task PersistTurnAsync(
        ChatRequestDto request,
        Guid answerId,
        PromptTemplateDefinition template,
        PreparedChatTurn preparedTurn,
        CancellationToken ct)
    {
        await _chatSessionStore.AppendTurnAsync(new ChatSessionTurnRecord
        {
            SessionId = request.SessionId,
            TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
            UserId = ParseUserId(_requestContextAccessor.UserId),
            AnswerId = answerId,
            UserMessage = request.Message,
            AssistantMessage = preparedTurn.ResponseMessage,
            Citations = preparedTurn.Citations,
            Usage = preparedTurn.Usage,
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            TimestampUtc = DateTime.UtcNow
        }, ct);
    }

    private static IReadOnlyList<RetrievedChunkDto> SelectEvidenceChunks(string message, IReadOnlyCollection<RetrievedChunkDto> chunks, int maxContextChunks)
    {
        if (chunks.Count == 0)
        {
            return Array.Empty<RetrievedChunkDto>();
        }

        var queryTerms = TokenizeEvidenceTerms(message);
        var ranked = chunks
            .Where(IsReadableChunk)
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreEvidenceChunk(chunk, queryTerms)
            })
            .Where(item => item.Score >= (queryTerms.Count == 0 ? 0.15 : 0.35))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Chunk.Score)
            .Take(maxContextChunks)
            .Select(item => item.Chunk)
            .ToList();

        return ranked;
    }

    private static double ScoreEvidenceChunk(RetrievedChunkDto chunk, HashSet<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return Math.Min(1, chunk.Score * 0.2);
        }

        var contentTerms = TokenizeEvidenceTerms($"{chunk.DocumentTitle} {chunk.Section} {chunk.Content}");
        var overlap = contentTerms.Count(queryTerms.Contains);
        var coverage = overlap / (double)Math.Max(1, Math.Min(queryTerms.Count, 6));
        var score = coverage + Math.Min(1, chunk.Score) * 0.2;

        if (chunk.DocumentTitle.ContainsAny(queryTerms))
        {
            score += 0.15;
        }

        if (!string.IsNullOrWhiteSpace(chunk.Section) && chunk.Section.ContainsAny(queryTerms))
        {
            score += 0.1;
        }

        return Math.Round(score, 4);
    }

    private static HashSet<string> TokenizeEvidenceTerms(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 3)
            .Where(term => !EvidenceStopWords.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsReadableChunk(RetrievedChunkDto chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk.Content) || PdfTextExtraction.LooksLikeArtifactText(chunk.Content))
        {
            return false;
        }

        var text = chunk.Content.Trim();
        var lettersOrDigits = text.Count(char.IsLetterOrDigit);
        var controlCharacters = text.Count(character => char.IsControl(character) && !char.IsWhiteSpace(character));
        var punctuationNoise = text.Count(character => !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character) && ".,;:!?%()[]{}-_/\\\"'".IndexOf(character) < 0);

        return lettersOrDigits >= 24
            && controlCharacters == 0
            && punctuationNoise / (double)Math.Max(1, text.Length) < 0.12;
    }

    private static IEnumerable<string> SegmentForStreaming(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var segments = SentenceBoundaryRegex.Split(text.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment));

        foreach (var segment in segments)
        {
            if (segment.Length <= 96)
            {
                yield return segment.EndsWith(' ') ? segment : $"{segment} ";
                continue;
            }

            var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var builder = new StringBuilder();
            foreach (var word in words)
            {
                if (builder.Length > 0 && builder.Length + word.Length + 1 > 72)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(word);
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
                yield return builder.ToString();
            }
        }
    }

    private static string ResolveExecutionMode(bool hasEvidence, bool allowsGeneralKnowledge)
    {
        if (hasEvidence)
        {
            return allowsGeneralKnowledge ? "auto-hybrid" : "auto-rag";
        }

        return allowsGeneralKnowledge ? "auto-llm" : "grounded";
    }

    private static string ResolveRetrievalStrategy(bool attemptedRetrieval, string retrievalStrategy, int evidenceChunkCount, bool allowsGeneralKnowledge)
    {
        if (evidenceChunkCount > 0)
        {
            return allowsGeneralKnowledge ? $"auto-hybrid:{retrievalStrategy}" : $"auto-rag:{retrievalStrategy}";
        }

        if (attemptedRetrieval)
        {
            return allowsGeneralKnowledge ? "auto-llm:fallback-after-retrieval" : retrievalStrategy;
        }

        return allowsGeneralKnowledge ? "auto-llm" : "grounded";
    }

    private static bool HasExplicitRetrievalScope(ChatFiltersDto? filters)
    {
        if (filters is null)
        {
            return false;
        }

        return filters.DocumentIds is { Count: > 0 }
            || filters.Tags is { Count: > 0 }
            || filters.Categories is { Count: > 0 }
            || filters.ContentTypes is { Count: > 0 }
            || filters.Sources is { Count: > 0 };
    }

    private PromptTemplateDefinition ResolveTemplate(ChatRequestDto request)
    {
        if (_promptInjectionDetector.TryDetect(request.Message, out var pattern))
        {
            _securityAuditLogger.LogPromptInjectionDetected($"chat:{request.SessionId}", $"Matched blocked pattern '{pattern}'.");
            ChatbotTelemetry.PromptInjectionSignals.Add(1);
            throw new InvalidOperationException("Potential prompt injection detected.");
        }

        // Se o request nao especificar um template, usa o mesmo padrao do contrato da API.
        var templateId = string.IsNullOrWhiteSpace(request.TemplateId)
            ? ChatDefaults.DefaultTemplateId
            : request.TemplateId;

        var templateVersion = string.IsNullOrWhiteSpace(request.TemplateVersion)
            ? null
            : request.TemplateVersion;

        return _promptTemplateRegistry.GetRequired(templateId, templateVersion);
    }

    private static Guid ParseUserId(string? rawUserId)
    {
        return Guid.TryParse(rawUserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }

    private static string BuildChatCompletionCacheKey(
        ChatRequestDto request,
        PromptTemplateDefinition template,
        AgenticChatPlan plan,
        IReadOnlyCollection<RetrievedChunkDto> chunks)
    {
        var fingerprint = string.Join('|', chunks
            .Select(chunk => $"{chunk.ChunkId}:{chunk.DocumentTitle}:{chunk.PageNumber}:{chunk.EndPageNumber}:{chunk.Section}:{ComputeHash(chunk.Content)}:{chunk.Score.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}")
            .OrderBy(value => value, StringComparer.Ordinal));

        return $"chat-completion:{ComputeHash(string.Join("||", new[]
        {
            request.Message.Trim(),
            template.TemplateId,
            template.Version,
            plan.ExecutionMode,
            plan.AllowsGeneralKnowledge.ToString(),
            fingerprint
        }))}";
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

file static class ChatEvidenceExtensions
{
    public static bool ContainsAny(this string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Serviço de recuperação
/// </summary>
public class RetrievalService : IRetrievalService
{
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IApplicationCache _applicationCache;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        ISearchIndexGateway indexGateway,
        IEmbeddingProvider embeddingProvider,
        IDocumentCatalog documentCatalog,
        IDocumentAuthorizationService documentAuthorizationService,
        IRequestContextAccessor requestContextAccessor,
        IApplicationCache applicationCache,
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        ILogger<RetrievalService> logger)
    {
        _indexGateway = indexGateway;
        _embeddingProvider = embeddingProvider;
        _documentCatalog = documentCatalog;
        _documentAuthorizationService = documentAuthorizationService;
        _requestContextAccessor = requestContextAccessor;
        _applicationCache = applicationCache;
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _logger = logger;
    }

    public async Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving documents for query: {query}", query.Query);
        using var activity = ChatbotTelemetry.ActivitySource.StartActivity("retrieval.query");
        activity?.SetTag("retrieval.top_k", query.TopK);

        var filters = new FileSearchFilterDto
        {
            DocumentIds = query.DocumentIds,
            Tags = query.Tags,
            Categories = query.Categories,
            ContentTypes = query.ContentTypes,
            Sources = query.Sources,
            TenantId = _requestContextAccessor.TenantId
        };
        var requestedTopK = Math.Max(1, query.TopK);
        var candidateCount = Math.Min(
            _ragRuntimeSettings.RetrievalMaxCandidateCount,
            Math.Max(requestedTopK, requestedTopK * _ragRuntimeSettings.RetrievalCandidateMultiplier));
        var semanticRankingEnabled = _featureFlagService.IsSemanticRankingEnabled && query.SemanticRanking;
        var cacheKey = BuildRetrievalCacheKey(query.Query, requestedTopK, candidateCount, semanticRankingEnabled, filters);

        var cached = await _applicationCache.GetAsync<RetrievalResultDto>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var queryEmbedding = string.IsNullOrWhiteSpace(query.Query)
            ? null
            : await _embeddingProvider.CreateEmbeddingAsync(query.Query, null, ct);

        var startTime = DateTime.UtcNow;
        var results = await _indexGateway.HybridSearchAsync(query.Query, queryEmbedding, candidateCount, filters, ct);
        var elapsed = DateTime.UtcNow - startTime;
        ChatbotTelemetry.RetrievalLatencyMs.Record(elapsed.TotalMilliseconds);

        var authorizedResults = results
            .Where(result =>
            {
                var document = _documentCatalog.Get(result.DocumentId);
                return document is not null && _documentAuthorizationService.CanAccess(
                    document,
                    _requestContextAccessor.TenantId,
                    _requestContextAccessor.UserId,
                    _requestContextAccessor.UserRole);
            })
            .ToList();

        var rerankedResults = authorizedResults
            .Select(result => new
            {
                Result = result,
                Score = ComputeRerankedScore(query.Query, result, filters)
            })
            .Where(item => item.Score >= _ragRuntimeSettings.MinimumRerankScore)
            .OrderByDescending(item => item.Score)
            .Take(requestedTopK)
            .ToList();

        if (rerankedResults.Count == 0)
        {
            rerankedResults = authorizedResults
                .OrderByDescending(result => result.Score)
                .Take(requestedTopK)
                .Select(result => new { Result = result, Score = result.Score })
                .ToList();
        }

        var retrievalResult = new RetrievalResultDto
        {
            Chunks = rerankedResults
                .Select(item => new RetrievedChunkDto
                {
                    ChunkId = item.Result.ChunkId,
                    DocumentId = item.Result.DocumentId,
                    Content = item.Result.Content,
                    Score = item.Score,
                    DocumentTitle = item.Result.Metadata.ContainsKey("title") ? item.Result.Metadata["title"] : "Unknown",
                    PageNumber = ParseMetadataInt(item.Result.Metadata, "startPage"),
                    EndPageNumber = ParseMetadataInt(item.Result.Metadata, "endPage"),
                    Section = item.Result.Metadata.TryGetValue("section", out var sectionValue) ? sectionValue : string.Empty
                })
                .ToList(),
            RetrievalStrategy = semanticRankingEnabled ? "hybrid-semantic-reranked" : "hybrid-reranked",
            LatencyMs = (long)elapsed.TotalMilliseconds
        };

        await _applicationCache.SetAsync(cacheKey, retrievalResult, _ragRuntimeSettings.RetrievalCacheTtl, ct);
        return retrievalResult;
    }

    public async Task<SearchQueryResponseDto> QueryAsync(SearchQueryRequestDto query, CancellationToken ct)
    {
        var retrievalResult = await RetrieveAsync(new RetrievalQueryDto
        {
            Query = query.Query,
            TopK = query.Top,
            DocumentIds = query.Filters?.DocumentIds,
            Tags = query.Filters?.Tags,
            Categories = query.Filters?.Categories,
            ContentTypes = query.Filters?.ContentTypes,
            Sources = query.Filters?.Sources,
            SemanticRanking = query.SemanticRanking
        }, ct);

        return new SearchQueryResponseDto
        {
            Items = retrievalResult.Chunks.Select(chunk => new SearchQueryItemDto
            {
                DocumentId = chunk.DocumentId,
                ChunkId = chunk.ChunkId,
                Title = chunk.DocumentTitle,
                Snippet = chunk.Content[..Math.Min(200, chunk.Content.Length)],
                Score = chunk.Score
            }).ToList(),
            Count = retrievalResult.Chunks.Count
        };
    }

    private string BuildRetrievalCacheKey(
        string query,
        int requestedTopK,
        int candidateCount,
        bool semanticRankingEnabled,
        FileSearchFilterDto filters)
    {
        var tenantId = _requestContextAccessor.TenantId?.ToString() ?? string.Empty;
        var userId = _requestContextAccessor.UserId ?? string.Empty;
        var userRole = _requestContextAccessor.UserRole ?? string.Empty;
        var documentIds = filters.DocumentIds is { Count: > 0 }
            ? string.Join(',', filters.DocumentIds.OrderBy(id => id))
            : string.Empty;
        var tags = filters.Tags is { Count: > 0 }
            ? string.Join(',', filters.Tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var categories = filters.Categories is { Count: > 0 }
            ? string.Join(',', filters.Categories.OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var contentTypes = filters.ContentTypes is { Count: > 0 }
            ? string.Join(',', filters.ContentTypes.OrderBy(contentType => contentType, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var sources = filters.Sources is { Count: > 0 }
            ? string.Join(',', filters.Sources.OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var coherencyStamp = ResolveCacheCoherencyStamp(filters);

        return $"retrieval:{ComputeHash(string.Join("||", new[]
        {
            query.Trim(),
            requestedTopK.ToString(),
            candidateCount.ToString(),
            semanticRankingEnabled.ToString(),
            tenantId,
            userId,
            userRole,
            documentIds,
            tags,
            categories,
            contentTypes,
            sources,
            coherencyStamp
        }))}";
    }

    private string ResolveCacheCoherencyStamp(FileSearchFilterDto filters)
    {
        var scopedDocuments = _documentCatalog.Query(new FileSearchFilterDto
        {
            TenantId = filters.TenantId,
            DocumentIds = filters.DocumentIds,
            Tags = filters.Tags,
            Categories = filters.Categories,
            ContentTypes = filters.ContentTypes,
            Sources = filters.Sources
        });

        if (scopedDocuments.Count == 0)
        {
            return "empty-scope";
        }

        return ComputeHash(string.Join('|', scopedDocuments
            .OrderBy(document => document.DocumentId)
            .Select(document => $"{document.DocumentId:N}:{document.Version}:{document.UpdatedAtUtc.Ticks}")));
    }

    private double ComputeRerankedScore(string query, SearchResultDto result, FileSearchFilterDto filters)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terms.Length == 0)
        {
            return result.Score;
        }

        var title = result.Metadata.TryGetValue("title", out var titleValue) ? titleValue : string.Empty;
        var tags = GetMetadataValues(result.Metadata, "tags");
        var categories = GetMetadataValues(result.Metadata, "categories");
        var category = result.Metadata.TryGetValue("category", out var categoryValue) ? categoryValue : string.Empty;
        var contentType = result.Metadata.TryGetValue("contentType", out var contentTypeValue) ? contentTypeValue : string.Empty;
        var source = result.Metadata.TryGetValue("source", out var sourceValue) ? sourceValue : string.Empty;
        var exactMatches = terms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase));
        var titleMatches = terms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase));
        var filterMatches = 0;

        if (filters.Tags is { Count: > 0 } && filters.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        if (filters.Categories is { Count: > 0 }
            && (filters.Categories.Any(filter => categories.Contains(filter, StringComparer.OrdinalIgnoreCase))
                || filters.Categories.Any(filter => string.Equals(filter, category, StringComparison.OrdinalIgnoreCase))))
        {
            filterMatches++;
        }

        if (filters.ContentTypes is { Count: > 0 }
            && filters.ContentTypes.Any(filter => string.Equals(filter, contentType, StringComparison.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        if (filters.Sources is { Count: > 0 }
            && filters.Sources.Any(filter => string.Equals(filter, source, StringComparison.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        var score = result.Score + (exactMatches / (double)terms.Length) * _ragRuntimeSettings.ExactMatchBoost;
        if (titleMatches)
        {
            score += _ragRuntimeSettings.TitleMatchBoost;
        }

        if (filterMatches > 0)
        {
            score += filterMatches * _ragRuntimeSettings.FilterMatchBoost;
        }

        return Math.Round(score, 4);
    }

    private static string[] GetMetadataValues(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue)
            ? rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();
    }

    private static int ParseMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : 1;
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

/// <summary>
/// Serviço de ingestão
/// </summary>
public class IngestionService : IIngestionPipeline
{
    private readonly IBlobStorageGateway _blobGateway;
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IMalwareScanner _malwareScanner;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IBlobStorageGateway blobGateway,
        IDocumentTextExtractor documentTextExtractor,
        IMalwareScanner malwareScanner,
        IEmbeddingProvider embeddingProvider,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IBackgroundJobQueue backgroundJobQueue,
        IDocumentAuthorizationService documentAuthorizationService,
        IPromptInjectionDetector promptInjectionDetector,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<IngestionService> logger)
    {
        _blobGateway = blobGateway;
        _documentTextExtractor = documentTextExtractor;
        _malwareScanner = malwareScanner;
        _embeddingProvider = embeddingProvider;
        _indexGateway = indexGateway;
        _chunkingStrategy = chunkingStrategy;
        _documentCatalog = documentCatalog;
        _requestContextAccessor = requestContextAccessor;
        _backgroundJobQueue = backgroundJobQueue;
        _documentAuthorizationService = documentAuthorizationService;
        _promptInjectionDetector = promptInjectionDetector;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
    }

    public async Task<UploadDocumentResponseDto> IngestAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        _logger.LogInformation("Starting ingestion for document {documentId}", command.DocumentId);
        var payload = await ReadContentAsync(command.Content, ct);
        var rawHash = ComputeHash(payload);
        var duplicate = _documentCatalog.FindByContentHash(command.TenantId, rawHash);
        if (duplicate is not null)
        {
            throw new DuplicateDocumentException($"A document with the same content already exists for this tenant: {duplicate.DocumentId}");
        }

        var jobId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var storagePath = $"documents/{command.TenantId}/{command.DocumentId}/raw-content";
        await _blobGateway.SaveAsync(new MemoryStream(payload, writable: false), storagePath, ct);

        var malwareResult = await _malwareScanner.ScanAsync(CloneCommand(command, payload), ct);
        if (!malwareResult.IsSafe)
        {
            var quarantinePath = malwareResult.RequiresQuarantine
                ? $"quarantine/{command.TenantId}/{command.DocumentId}/{command.FileName}"
                : null;

            if (quarantinePath is not null)
            {
                await _blobGateway.SaveAsync(new MemoryStream(payload, writable: false), quarantinePath, ct);
            }

            _documentCatalog.Upsert(new DocumentCatalogEntry
            {
                DocumentId = command.DocumentId,
                TenantId = command.TenantId,
                Title = string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle,
                ContentType = string.IsNullOrWhiteSpace(command.ContentType) ? "application/octet-stream" : command.ContentType,
                Source = command.Source,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp,
                Status = "Failed",
                Version = 1,
                ContentHash = rawHash,
                Tags = command.Tags,
                Categories = command.Categories,
                Category = command.Category,
                ExternalId = command.ExternalId,
                AccessPolicy = command.AccessPolicy,
                StoragePath = storagePath,
                QuarantinePath = quarantinePath,
                LastJobId = jobId,
                Chunks = new List<DocumentChunkIndexDto>()
            });

            throw new InvalidOperationException(malwareResult.Reason ?? "File rejected by malware scan.");
        }

        _documentCatalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = command.DocumentId,
            TenantId = command.TenantId,
            Title = string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle,
            OriginalFileName = command.FileName,
            ContentType = string.IsNullOrWhiteSpace(command.ContentType) ? "application/octet-stream" : command.ContentType,
            Source = command.Source,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
            Status = "Queued",
            Version = 1,
            ContentHash = rawHash,
            Tags = command.Tags,
            Categories = command.Categories,
            Category = command.Category,
            ExternalId = command.ExternalId,
            AccessPolicy = command.AccessPolicy,
            StoragePath = storagePath,
            LastJobId = jobId,
            Chunks = new List<DocumentChunkIndexDto>()
        });

        await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
        {
            var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
            await processor.ProcessIngestionAsync(new IngestionBackgroundJob
            {
                JobId = jobId,
                DocumentId = command.DocumentId,
                TenantId = command.TenantId,
                FileName = command.FileName,
                ContentType = command.ContentType,
                ContentLength = command.ContentLength,
                DocumentTitle = command.DocumentTitle,
                Category = command.Category,
                Tags = new List<string>(command.Tags),
                Categories = new List<string>(command.Categories),
                Source = command.Source,
                ExternalId = command.ExternalId,
                AccessPolicy = command.AccessPolicy,
                Payload = payload,
                RawHash = rawHash,
                StoragePath = storagePath
            }, jobCt);
        }, ct);
        ChatbotTelemetry.IngestionJobsQueued.Add(1);

        return new UploadDocumentResponseDto
        {
            DocumentId = command.DocumentId,
            Status = "Queued",
            IngestionJobId = jobId,
            TimestampUtc = timestamp,
            CreatedAtUtc = timestamp
        };
    }

    public async Task<ReindexDocumentResponseDto> ReindexAsync(Guid documentId, bool fullReindex, CancellationToken ct)
    {
        _logger.LogInformation("Reindexing document {documentId}, full: {isFull}", documentId, fullReindex);

        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document {documentId} not found");
        }

        EnsureTenantAccess(document);

        var jobId = Guid.NewGuid();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.Status = "ReindexPending";
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);

        await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
        {
            var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
            await processor.ProcessReindexAsync(new ReindexBackgroundJob
            {
                JobId = jobId,
                DocumentId = documentId,
                FullReindex = fullReindex,
                ForceEmbeddingModel = null
            }, jobCt);
        }, ct);
        ChatbotTelemetry.ReindexJobsQueued.Add(1);

        return new ReindexDocumentResponseDto
        {
            DocumentId = documentId,
            Status = "ReindexPending",
            ChunksReindexed = 0,
            JobId = jobId
        };
    }

    public async Task<BulkReindexResponseDto> ReindexAsync(BulkReindexRequestDto request, Guid tenantId, CancellationToken ct)
    {
        var jobId = Guid.NewGuid();
        var documents = ResolveBulkReindexDocuments(request, tenantId);

        foreach (var document in documents)
        {
            EnsureTenantAccess(document);
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.Status = "ReindexPending";
            document.LastJobId = jobId;
            _documentCatalog.Upsert(document);
        }

        foreach (var document in documents)
        {
            await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
            {
                var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
                await processor.ProcessReindexAsync(new ReindexBackgroundJob
                {
                    JobId = jobId,
                    DocumentId = document.DocumentId,
                    FullReindex = string.Equals(request.Mode, "full", StringComparison.OrdinalIgnoreCase),
                    ForceEmbeddingModel = request.ForceEmbeddingModel
                }, jobCt);
            }, ct);
        }
        ChatbotTelemetry.ReindexJobsQueued.Add(documents.Count);

        return new BulkReindexResponseDto
        {
            Accepted = true,
            JobId = jobId,
            Mode = request.Mode,
            DocumentCount = documents.Count
        };
    }

    private List<DocumentCatalogEntry> ResolveBulkReindexDocuments(BulkReindexRequestDto request, Guid tenantId)
    {
        if (request.IncludeAllTenantDocuments)
        {
            return _documentCatalog.Query(null)
                .Where(document => document.TenantId == tenantId)
                .Where(document => !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return request.DocumentIds
            .Select(documentId => _documentCatalog.Get(documentId))
            .Where(document => document is not null)
            .Cast<DocumentCatalogEntry>()
            .ToList();
    }

    public Task<DocumentDetailsDto?> GetDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return Task.FromResult<DocumentDetailsDto?>(null);
        }

        EnsureTenantAccess(document);

        return Task.FromResult<DocumentDetailsDto?>(new DocumentDetailsDto
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            Status = document.Status,
            Version = document.Version,
            ContentType = document.ContentType,
            Source = document.Source,
            LastJobId = document.LastJobId,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            Metadata = new DocumentMetadataDto
            {
                Category = document.Category,
                Tags = document.Tags,
                Categories = document.Categories,
                ExternalId = document.ExternalId,
                AccessPolicy = document.AccessPolicy
            }
        });
    }

    private void EnsureTenantAccess(DocumentCatalogEntry document)
    {
        var canAccess = _requestContextAccessor.TenantId.HasValue && _documentAuthorizationService.CanAccess(
            document,
            _requestContextAccessor.TenantId,
            _requestContextAccessor.UserId,
            _requestContextAccessor.UserRole);

        if (!canAccess)
        {
            _securityAuditLogger.LogAccessDenied(_requestContextAccessor.UserId, $"document:{document.DocumentId}");
            throw new UnauthorizedAccessException("Document does not belong to the current tenant.");
        }
    }

    private static IngestDocumentCommand CloneCommand(IngestDocumentCommand command, byte[] payload)
    {
        return new IngestDocumentCommand
        {
            DocumentId = command.DocumentId,
            TenantId = command.TenantId,
            FileName = command.FileName,
            ContentType = command.ContentType,
            ContentLength = command.ContentLength,
            DocumentTitle = command.DocumentTitle,
            Category = command.Category,
            Tags = new List<string>(command.Tags),
            Categories = new List<string>(command.Categories),
            Source = command.Source,
            ExternalId = command.ExternalId,
            AccessPolicy = command.AccessPolicy,
            Content = new MemoryStream(payload, writable: false)
        };
    }

    private static async Task<byte[]> ReadContentAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return buffer.ToArray();
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content));
    }

}

public sealed class IngestionJobProcessor : IIngestionJobProcessor
{
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IBlobStorageGateway _blobGateway;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<IngestionJobProcessor> _logger;

    public IngestionJobProcessor(
        IDocumentTextExtractor documentTextExtractor,
        IEmbeddingProvider embeddingProvider,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IBlobStorageGateway blobGateway,
        IPromptInjectionDetector promptInjectionDetector,
        ISecurityAuditLogger securityAuditLogger,
        ResiliencePipeline resiliencePipeline,
        ILogger<IngestionJobProcessor> logger)
    {
        _documentTextExtractor = documentTextExtractor;
        _embeddingProvider = embeddingProvider;
        _indexGateway = indexGateway;
        _chunkingStrategy = chunkingStrategy;
        _documentCatalog = documentCatalog;
        _blobGateway = blobGateway;
        _promptInjectionDetector = promptInjectionDetector;
        _securityAuditLogger = securityAuditLogger;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task ProcessIngestionAsync(IngestionBackgroundJob job, CancellationToken ct)
    {
        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("ingestion.process");
            activity?.SetTag("document.id", job.DocumentId);
            var startedAt = DateTime.UtcNow;
            UpdateDocumentStatus(job.DocumentId, "Parsing", job.JobId);

            var extracted = await _resiliencePipeline.ExecuteAsync(async token =>
                await _documentTextExtractor.ExtractAsync(ToCommand(job), token), ct);
            if (string.Equals(extracted.Strategy, "ocr", StringComparison.OrdinalIgnoreCase))
            {
                UpdateDocumentStatus(job.DocumentId, "OcrProcessing", job.JobId);
            }

            var extraction = EnsureExtractionHasContent(extracted, job.FileName);
            var normalizedText = extraction.Text;

            if (_promptInjectionDetector.TryDetect(normalizedText, out var pattern))
            {
                _securityAuditLogger.LogPromptInjectionDetected($"document:{job.DocumentId}", $"Matched blocked pattern '{pattern}'.");
                ChatbotTelemetry.PromptInjectionSignals.Add(1);
            }

            UpdateDocumentStatus(job.DocumentId, "Chunking", job.JobId);
            var chunks = _chunkingStrategy.Chunk(ToCommand(job), extraction);
            await EnrichChunksAsync(chunks, null, false, ct);

            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
                await _indexGateway.IndexDocumentChunksAsync(chunks, token);
            }, ct);

            var document = _documentCatalog.Get(job.DocumentId);
            if (document is null)
            {
                return;
            }

            document.Status = "Indexed";
            document.StoragePath = job.StoragePath;
            document.QuarantinePath = null;
            document.ContentHash = job.RawHash;
            document.OriginalFileName = job.FileName;
            document.Chunks = chunks;
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.LastJobId = job.JobId;
            _documentCatalog.Upsert(document);
            ChatbotTelemetry.IngestionLatencyMs.Record((DateTime.UtcNow - startedAt).TotalMilliseconds);

            _logger.LogInformation("Document {documentId} indexed with {chunkCount} chunks", job.DocumentId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting document {documentId}", job.DocumentId);
            var failed = _documentCatalog.Get(job.DocumentId);
            if (failed is not null)
            {
                failed.Status = "Failed";
                failed.UpdatedAtUtc = DateTime.UtcNow;
                failed.LastJobId = job.JobId;
                _documentCatalog.Upsert(failed);
            }
        }
    }

    public async Task ProcessReindexAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("reindex.process");
            activity?.SetTag("document.id", job.DocumentId);
            if (job.FullReindex)
            {
                await ProcessFullReindexAsync(job, ct);
                return;
            }

            await Task.Delay(job.FullReindex ? 150 : 75, ct);
            var document = _documentCatalog.Get(job.DocumentId);
            if (document is null)
            {
                return;
            }

            var updatedEmbeddings = 0;
            if (document.Chunks.Count > 0)
            {
                foreach (var chunk in document.Chunks)
                {
                    if (chunk.Embedding is { Length: > 0 } && string.IsNullOrWhiteSpace(job.ForceEmbeddingModel))
                    {
                        ChatbotTelemetry.EmbeddingReuse.Add(1, new KeyValuePair<string, object?>("reuse.kind", "existing-chunk"));
                        continue;
                    }

                    chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                        await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, job.ForceEmbeddingModel, token), ct);
                    chunk.Metadata["embeddingModel"] = string.IsNullOrWhiteSpace(job.ForceEmbeddingModel) ? "default" : job.ForceEmbeddingModel;
                    chunk.Metadata["contentHash"] = ComputeHash(chunk.Content);
                    updatedEmbeddings++;
                }

                if (updatedEmbeddings > 0)
                {
                    await _resiliencePipeline.ExecuteAsync(async token =>
                        await _indexGateway.IndexDocumentChunksAsync(document.Chunks, token), ct);
                }
            }

            document.Version += 1;
            document.Status = "Indexed";
            document.UpdatedAtUtc = DateTime.UtcNow;
            _documentCatalog.Upsert(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reindexing document {documentId}", job.DocumentId);
            var document = _documentCatalog.Get(job.DocumentId);
            if (document is not null)
            {
                document.Status = "Failed";
                document.UpdatedAtUtc = DateTime.UtcNow;
                _documentCatalog.Upsert(document);
            }
        }
    }

    private async Task ProcessFullReindexAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        var document = _documentCatalog.Get(job.DocumentId);
        if (document is null || string.IsNullOrWhiteSpace(document.StoragePath))
        {
            return;
        }

        UpdateDocumentStatus(job.DocumentId, "Parsing", job.JobId);
        await using var content = await _blobGateway.GetAsync(document.StoragePath, ct);
        var payload = await ReadContentAsync(content, ct);
        var command = BuildReindexCommand(document, payload);
        var extracted = await _resiliencePipeline.ExecuteAsync(async token =>
            await _documentTextExtractor.ExtractAsync(command, token), ct);
        var extraction = EnsureExtractionHasContent(extracted, command.FileName);
        var normalizedText = extraction.Text;

        if (_promptInjectionDetector.TryDetect(normalizedText, out var pattern))
        {
            _securityAuditLogger.LogPromptInjectionDetected($"document:{job.DocumentId}", $"Matched blocked pattern '{pattern}'.");
            ChatbotTelemetry.PromptInjectionSignals.Add(1);
        }

        UpdateDocumentStatus(job.DocumentId, "Chunking", job.JobId);
        var chunks = _chunkingStrategy.Chunk(command, extraction);
        await EnrichChunksAsync(chunks, job.ForceEmbeddingModel, false, ct);

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
            await _indexGateway.IndexDocumentChunksAsync(chunks, token);
        }, ct);

        document.Version += 1;
        document.Status = "Indexed";
        document.ContentHash = ComputeHash(payload);
        document.ContentType = command.ContentType;
        document.OriginalFileName = command.FileName;
        document.Chunks = chunks;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = job.JobId;
        _documentCatalog.Upsert(document);
    }

    private static IngestDocumentCommand ToCommand(IngestionBackgroundJob job)
    {
        return new IngestDocumentCommand
        {
            DocumentId = job.DocumentId,
            TenantId = job.TenantId,
            FileName = job.FileName,
            ContentType = job.ContentType,
            ContentLength = job.ContentLength,
            DocumentTitle = job.DocumentTitle,
            Category = job.Category,
            Tags = new List<string>(job.Tags),
            Categories = new List<string>(job.Categories),
            Source = job.Source,
            ExternalId = job.ExternalId,
            AccessPolicy = job.AccessPolicy,
            Content = new MemoryStream(job.Payload, writable: false)
        };
    }

    private void UpdateDocumentStatus(Guid documentId, string status, Guid jobId)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return;
        }

        document.Status = status;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);
    }

    private async Task EnrichChunksAsync(
        List<DocumentChunkIndexDto> chunks,
        string? forceEmbeddingModel,
        bool forceRefresh,
        CancellationToken ct)
    {
        foreach (var chunk in chunks)
        {
            chunk.Metadata["contentHash"] = ComputeHash(chunk.Content);
            if (!forceRefresh && chunk.Embedding is { Length: > 0 } && string.IsNullOrWhiteSpace(forceEmbeddingModel))
            {
                ChatbotTelemetry.EmbeddingReuse.Add(1, new KeyValuePair<string, object?>("reuse.kind", "existing-chunk"));
                continue;
            }

            chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, forceEmbeddingModel, token), ct);
            chunk.Metadata["embeddingModel"] = string.IsNullOrWhiteSpace(forceEmbeddingModel) ? "default" : forceEmbeddingModel;
        }
    }

    private static IngestDocumentCommand BuildReindexCommand(DocumentCatalogEntry document, byte[] payload)
    {
        var fileName = string.IsNullOrWhiteSpace(document.OriginalFileName)
            ? BuildFallbackFileName(document)
            : document.OriginalFileName;

        return new IngestDocumentCommand
        {
            DocumentId = document.DocumentId,
            TenantId = document.TenantId,
            FileName = fileName,
            ContentType = document.ContentType,
            ContentLength = payload.LongLength,
            DocumentTitle = document.Title,
            Category = document.Category,
            Tags = new List<string>(document.Tags),
            Categories = new List<string>(document.Categories),
            Source = document.Source,
            ExternalId = document.ExternalId,
            AccessPolicy = document.AccessPolicy,
            Content = new MemoryStream(payload, writable: false)
        };
    }

    private static string BuildFallbackFileName(DocumentCatalogEntry document)
    {
        var extension = document.ContentType switch
        {
            "application/pdf" => ".pdf",
            "text/plain" => ".txt",
            "text/markdown" => ".md",
            "text/html" => ".html",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            _ => ".bin"
        };

        return $"{document.Title}{extension}";
    }

    private static async Task<byte[]> ReadContentAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return buffer.ToArray();
    }

    private static DocumentTextExtractionResultDto EnsureExtractionHasContent(DocumentTextExtractionResultDto extracted, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(extracted.Text))
        {
            return extracted;
        }

        var fallbackText = $"Conteudo indisponivel para {fileName}";
        return new DocumentTextExtractionResultDto
        {
            Text = fallbackText,
            Strategy = extracted.Strategy,
            Provider = extracted.Provider,
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = fallbackText
                }
            }
        };
    }

    private static int ParseMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : 1;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content));
    }
}
