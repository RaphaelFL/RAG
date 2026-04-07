using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

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
