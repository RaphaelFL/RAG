using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Security.Cryptography;
using System.Text;

namespace Chatbot.Application.Services;

/// <summary>
/// Serviço de orquestração de chat
/// </summary>
public class ChatOrchestratorService : IChatOrchestrator
{
    private readonly IRetrievalService _retrievalService;
    private readonly ICitationAssembler _citationAssembler;
    private readonly IAgenticChatPlanner _agenticChatPlanner;
    private readonly IChatCompletionProvider _chatCompletionProvider;
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly IChatSessionStore _chatSessionStore;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly IFeatureFlagService _featureFlagService;
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
        IFeatureFlagService featureFlagService,
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
        _featureFlagService = featureFlagService;
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

            // Retrieve relevant documents
            var retrievalResult = plan.RequiresRetrieval
                ? await _resiliencePipeline.ExecuteAsync(async token => await _retrievalService.RetrieveAsync(new RetrievalQueryDto
                {
                    Query = request.Message,
                    TopK = request.Options?.MaxCitations ?? 5,
                    DocumentIds = request.Filters?.DocumentIds,
                    Tags = request.Filters?.Tags,
                    Categories = request.Filters?.Categories,
                    SemanticRanking = _featureFlagService.IsSemanticRankingEnabled && (request.Options?.SemanticRanking ?? true)
                }, token), ct)
                : new RetrievalResultDto
                {
                    Chunks = new List<RetrievedChunkDto>(),
                    RetrievalStrategy = "agentic-general-knowledge",
                    LatencyMs = 0
                };
            var citations = _citationAssembler.Assemble(retrievalResult.Chunks, request.Options?.MaxCitations ?? 5);
            ChatCompletionResult? completionResult = null;
            var responseMessage = template.InsufficientEvidenceMessage;
            if (retrievalResult.Chunks.Count > 0 || plan.AllowsGeneralKnowledge)
            {
                completionResult = await _resiliencePipeline.ExecuteAsync(async token => await _chatCompletionProvider.CompleteAsync(new ChatCompletionRequest
                {
                    Message = request.Message,
                    Template = template,
                    AllowGeneralKnowledge = plan.AllowsGeneralKnowledge,
                    RetrievedChunks = retrievalResult.Chunks
                }, token), ct);
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
                RetrievalStrategy = retrievalResult.RetrievalStrategy
            };

            await _chatSessionStore.AppendTurnAsync(new ChatSessionTurnRecord
            {
                SessionId = request.SessionId,
                TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
                UserId = ParseUserId(_requestContextAccessor.UserId),
                AnswerId = answerId,
                UserMessage = request.Message,
                AssistantMessage = responseMessage,
                Citations = citations,
                Usage = usage,
                TemplateId = template.TemplateId,
                TemplateVersion = template.Version,
                TimestampUtc = DateTime.UtcNow
            }, ct);

            return new ChatResponseDto
            {
                AnswerId = answerId,
                SessionId = request.SessionId,
                Message = responseMessage,
                Citations = citations,
                Usage = usage,
                Policy = new ChatPolicyDto
                {
                    Grounded = plan.RequiresRetrieval && retrievalResult.Chunks.Count > 0,
                    HadEnoughEvidence = retrievalResult.Chunks.Count > 0 || plan.AllowsGeneralKnowledge,
                    TemplateId = template.TemplateId,
                    TemplateVersion = template.Version
                },
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

        // Retrieve documents
        var retrievalResult = plan.RequiresRetrieval
            ? await _resiliencePipeline.ExecuteAsync(async token => await _retrievalService.RetrieveAsync(new RetrievalQueryDto
            {
                Query = request.Message,
                TopK = request.Options?.MaxCitations ?? 5,
                DocumentIds = request.Filters?.DocumentIds,
                Tags = request.Filters?.Tags,
                Categories = request.Filters?.Categories,
                SemanticRanking = _featureFlagService.IsSemanticRankingEnabled && (request.Options?.SemanticRanking ?? true)
            }, token), ct)
            : new RetrievalResultDto
            {
                Chunks = new List<RetrievedChunkDto>(),
                RetrievalStrategy = "agentic-general-knowledge",
                LatencyMs = 0
            };
        var citations = _citationAssembler.Assemble(retrievalResult.Chunks, request.Options?.MaxCitations ?? 5);

        ChatCompletionResult? completionResult = null;
        var responseText = template.InsufficientEvidenceMessage;
        if (retrievalResult.Chunks.Count > 0 || plan.AllowsGeneralKnowledge)
        {
            completionResult = await _resiliencePipeline.ExecuteAsync(async token => await _chatCompletionProvider.CompleteAsync(new ChatCompletionRequest
            {
                Message = request.Message,
                Template = template,
                AllowGeneralKnowledge = plan.AllowsGeneralKnowledge,
                RetrievedChunks = retrievalResult.Chunks
            }, token), ct);
            responseText = string.IsNullOrWhiteSpace(completionResult.Message)
                ? template.InsufficientEvidenceMessage
                : completionResult.Message;
        }

        var words = responseText.Split(' ');

        foreach (var word in words)
        {
            await Task.Delay(50, ct); // Simulate streaming delay
            yield return new StreamingChatEventDto
            {
                EventType = "delta",
                Data = new StreamDeltaEventDto { Text = word + " " }
            };
        }

        // Send citations
        foreach (var citation in citations)
        {
            yield return new StreamingChatEventDto
            {
                EventType = "citation",
                Data = citation
            };
        }

        var usage = new UsageMetadataDto
        {
            Model = completionResult?.Model ?? "policy-only",
            PromptTokens = completionResult?.PromptTokens ?? 0,
            CompletionTokens = completionResult?.CompletionTokens ?? 0,
            TotalTokens = completionResult?.TotalTokens ?? 0,
            LatencyMs = completionResult is null ? 0 : 1000,
            RetrievalStrategy = retrievalResult.RetrievalStrategy
        };

        await _chatSessionStore.AppendTurnAsync(new ChatSessionTurnRecord
        {
            SessionId = request.SessionId,
            TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
            UserId = ParseUserId(_requestContextAccessor.UserId),
            AnswerId = answerId,
            UserMessage = request.Message,
            AssistantMessage = responseText,
            Citations = citations,
            Usage = usage,
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            TimestampUtc = DateTime.UtcNow
        }, ct);

        // Send completed event
        yield return new StreamingChatEventDto
        {
            EventType = "completed",
            Data = new StreamCompletedEventDto
            {
                Usage = usage
            }
        };
    }

    private PromptTemplateDefinition ResolveTemplate(ChatRequestDto request)
    {
        if (_promptInjectionDetector.TryDetect(request.Message, out var pattern))
        {
            _securityAuditLogger.LogPromptInjectionDetected($"chat:{request.SessionId}", $"Matched blocked pattern '{pattern}'.");
            ChatbotTelemetry.PromptInjectionSignals.Add(1);
            throw new InvalidOperationException("Potential prompt injection detected.");
        }

        return _promptTemplateRegistry.GetRequired(request.TemplateId, request.TemplateVersion);
    }

    private static Guid ParseUserId(string? rawUserId)
    {
        return Guid.TryParse(rawUserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }
}

/// <summary>
/// Serviço de recuperação
/// </summary>
public class RetrievalService : IRetrievalService
{
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        ISearchIndexGateway indexGateway,
        IDocumentCatalog documentCatalog,
        IDocumentAuthorizationService documentAuthorizationService,
        IRequestContextAccessor requestContextAccessor,
        IFeatureFlagService featureFlagService,
        ILogger<RetrievalService> logger)
    {
        _indexGateway = indexGateway;
        _documentCatalog = documentCatalog;
        _documentAuthorizationService = documentAuthorizationService;
        _requestContextAccessor = requestContextAccessor;
        _featureFlagService = featureFlagService;
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
            TenantId = _requestContextAccessor.TenantId
        };
        var semanticRankingEnabled = _featureFlagService.IsSemanticRankingEnabled && query.SemanticRanking;

        var startTime = DateTime.UtcNow;
        var results = await _indexGateway.HybridSearchAsync(query.Query, query.TopK, filters, ct);
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

        return new RetrievalResultDto
        {
            Chunks = authorizedResults
                .Select(r => new RetrievedChunkDto
                {
                    ChunkId = r.ChunkId,
                    DocumentId = r.DocumentId,
                    Content = r.Content,
                    Score = r.Score,
                    DocumentTitle = r.Metadata.ContainsKey("title") ? r.Metadata["title"] : "Unknown"
                })
                .ToList(),
            RetrievalStrategy = semanticRankingEnabled ? "hybrid-semantic" : "hybrid",
            LatencyMs = (long)elapsed.TotalMilliseconds
        };
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

    public async Task<BulkReindexResponseDto> ReindexAsync(BulkReindexRequestDto request, CancellationToken ct)
    {
        var jobId = Guid.NewGuid();
        foreach (var documentId in request.DocumentIds)
        {
            var document = _documentCatalog.Get(documentId);
            if (document is null)
            {
                continue;
            }

            EnsureTenantAccess(document);
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.Status = "ReindexPending";
            document.LastJobId = jobId;
            _documentCatalog.Upsert(document);
        }

        foreach (var documentId in request.DocumentIds)
        {
            await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
            {
                var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
                await processor.ProcessReindexAsync(new ReindexBackgroundJob
                {
                    JobId = jobId,
                    DocumentId = documentId,
                    FullReindex = string.Equals(request.Mode, "full", StringComparison.OrdinalIgnoreCase),
                    ForceEmbeddingModel = request.ForceEmbeddingModel
                }, jobCt);
            }, ct);
        }
        ChatbotTelemetry.ReindexJobsQueued.Add(request.DocumentIds.Count);

        return new BulkReindexResponseDto
        {
            Accepted = true,
            JobId = jobId,
            Mode = request.Mode
        };
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

            var normalizedText = string.IsNullOrWhiteSpace(extracted.Text)
                ? $"Conteudo indisponivel para {job.FileName}"
                : extracted.Text;

            if (_promptInjectionDetector.TryDetect(normalizedText, out var pattern))
            {
                _securityAuditLogger.LogPromptInjectionDetected($"document:{job.DocumentId}", $"Matched blocked pattern '{pattern}'.");
                ChatbotTelemetry.PromptInjectionSignals.Add(1);
            }

            UpdateDocumentStatus(job.DocumentId, "Chunking", job.JobId);
            var chunks = _chunkingStrategy.Chunk(ToCommand(job), normalizedText);
            foreach (var chunk in chunks)
            {
                chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, null, token), ct);
                chunk.Metadata["embeddingModel"] = "default";
            }

            await _resiliencePipeline.ExecuteAsync(async token =>
                await _indexGateway.IndexDocumentChunksAsync(chunks, token), ct);

            var document = _documentCatalog.Get(job.DocumentId);
            if (document is null)
            {
                return;
            }

            document.Status = "Indexed";
            document.StoragePath = job.StoragePath;
            document.QuarantinePath = null;
            document.ContentHash = job.RawHash;
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
            await Task.Delay(job.FullReindex ? 150 : 75, ct);
            var document = _documentCatalog.Get(job.DocumentId);
            if (document is null)
            {
                return;
            }

            if (document.Chunks.Count > 0)
            {
                foreach (var chunk in document.Chunks)
                {
                    chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                        await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, job.ForceEmbeddingModel, token), ct);
                    if (!string.IsNullOrWhiteSpace(job.ForceEmbeddingModel))
                    {
                        chunk.Metadata["embeddingModel"] = job.ForceEmbeddingModel;
                    }
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
