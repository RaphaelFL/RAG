using System.Security.Cryptography;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;
using InfraCfg = Chatbot.Infrastructure.Configuration;

namespace Chatbot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class RagPlatformController : ControllerBase
{
    private readonly IEmbeddingGenerationService _embeddingGenerationService;
    private readonly IRetriever _retriever;
    private readonly IPromptAssembler _promptAssembler;
    private readonly IWebSearchTool _webSearchTool;
    private readonly IFileSearchTool _fileSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IAgentRuntime _agentRuntime;
    private readonly IOperationalAuditStore _operationalAuditStore;
    private readonly AppCfg.EmbeddingGenerationOptions _embeddingOptions;
    private readonly AppCfg.VectorStoreOptions _vectorStoreOptions;
    private readonly AppCfg.AgentRuntimeOptions _agentRuntimeOptions;
    private readonly InfraCfg.FeatureFlagOptions _featureFlags;

    public RagPlatformController(
        IEmbeddingGenerationService embeddingGenerationService,
        IRetriever retriever,
        IPromptAssembler promptAssembler,
        IWebSearchTool webSearchTool,
        IFileSearchTool fileSearchTool,
        ICodeInterpreter codeInterpreter,
        IAgentRuntime agentRuntime,
        IOperationalAuditStore operationalAuditStore,
        IOptions<AppCfg.EmbeddingGenerationOptions> embeddingOptions,
        IOptions<AppCfg.VectorStoreOptions> vectorStoreOptions,
        IOptions<AppCfg.AgentRuntimeOptions> agentRuntimeOptions,
        IOptions<InfraCfg.FeatureFlagOptions> featureFlags)
    {
        _embeddingGenerationService = embeddingGenerationService;
        _retriever = retriever;
        _promptAssembler = promptAssembler;
        _webSearchTool = webSearchTool;
        _fileSearchTool = fileSearchTool;
        _codeInterpreter = codeInterpreter;
        _agentRuntime = agentRuntime;
        _operationalAuditStore = operationalAuditStore;
        _embeddingOptions = embeddingOptions.Value;
        _vectorStoreOptions = vectorStoreOptions.Value;
        _agentRuntimeOptions = agentRuntimeOptions.Value;
        _featureFlags = featureFlags.Value;
    }

    [HttpGet("capabilities")]
    public ActionResult<object> GetCapabilities()
    {
        return Ok(new
        {
            mcpEnabled = _featureFlags.EnableMcp,
            semanticRankingEnabled = _featureFlags.EnableSemanticRanking,
            graphRagEnabled = _featureFlags.EnableGraphRag,
            vectorStore = _vectorStoreOptions.Provider,
            embeddingModel = new { _embeddingOptions.ModelName, _embeddingOptions.ModelVersion, _embeddingOptions.Dimensions },
            agentRuntime = new { _agentRuntimeOptions.Enabled, _agentRuntimeOptions.MaxToolBudget, _agentRuntimeOptions.MaxDepth }
        });
    }

    [HttpPost("embeddings/generate")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<GenerateEmbeddingsResponseDtoV2>> GenerateEmbeddings([FromBody] GenerateEmbeddingsRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var inputs = request.Items.Select(item => new EmbeddingInput
        {
            ChunkId = string.IsNullOrWhiteSpace(item.ChunkId) ? Guid.NewGuid().ToString("N") : item.ChunkId,
            DocumentId = item.DocumentId,
            TenantId = tenantId,
            ContentHash = string.IsNullOrWhiteSpace(item.ContentHash) ? ComputeHash(item.Text) : item.ContentHash,
            Text = item.Text
        }).ToArray();

        var result = await _embeddingGenerationService.GenerateBatchAsync(new EmbeddingBatchRequest
        {
            EmbeddingModelName = request.EmbeddingModelName ?? _embeddingOptions.ModelName,
            EmbeddingModelVersion = request.EmbeddingModelVersion ?? _embeddingOptions.ModelVersion,
            Inputs = inputs
        }, cancellationToken);

        return Ok(new GenerateEmbeddingsResponseDtoV2
        {
            ModelName = request.EmbeddingModelName ?? _embeddingOptions.ModelName,
            ModelVersion = request.EmbeddingModelVersion ?? _embeddingOptions.ModelVersion,
            Dimensions = result.FirstOrDefault()?.VectorDimensions ?? _embeddingOptions.Dimensions,
            Items = result.Select(item => new GenerateEmbeddingItemResponseDtoV2
            {
                ChunkId = item.ChunkId,
                VectorDimensions = item.VectorDimensions,
                Vector = item.Vector
            }).ToList()
        });
    }

    [HttpPost("retrieval/query")]
    public async Task<ActionResult<RetrievalResponseDto>> QueryRetrieval([FromBody] RetrievalRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _retriever.RetrieveAsync(new RetrievalPlan
        {
            TenantId = GetTenantId(),
            QueryText = request.Query,
            TopK = request.TopK,
            MaxContextChunks = request.TopK,
            UseDenseRetrieval = true,
            UseHybridRetrieval = request.UseHybridRetrieval,
            UseReranking = request.UseReranking,
            Filters = request.Filters
        }, cancellationToken);

        return Ok(new RetrievalResponseDto
        {
            Strategy = result.RetrievalStrategy,
            LatencyMs = result.LatencyMs,
            Chunks = result.Chunks.Select(chunk => new RetrievedChunkDtoV2
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Text = chunk.Text,
                Score = chunk.Score,
                SourceName = chunk.Metadata.TryGetValue("documentTitle", out var title) ? title : string.Empty,
                Metadata = chunk.Metadata
            }).ToList()
        });
    }

    [HttpPost("prompt-assembly")]
    public async Task<ActionResult<PromptAssemblyResponseDto>> AssemblePrompt([FromBody] PromptAssemblyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _promptAssembler.AssembleAsync(new PromptAssemblyRequest
        {
            TenantId = GetTenantId(),
            SystemInstructions = request.SystemInstructions,
            UserQuestion = request.Question,
            MaxPromptTokens = request.MaxPromptTokens,
            AllowGeneralKnowledge = request.AllowGeneralKnowledge,
            Chunks = request.Chunks.Select(chunk => new RetrievedChunk
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Text = chunk.Text,
                Score = chunk.Score,
                Metadata = chunk.Metadata
            }).ToArray()
        }, cancellationToken);

        return Ok(new PromptAssemblyResponseDto
        {
            Prompt = result.Prompt,
            EstimatedPromptTokens = result.EstimatedPromptTokens,
            IncludedChunkIds = result.IncludedChunkIds.ToList(),
            Citations = result.HumanReadableCitations.ToList()
        });
    }

    [HttpPost("file-search")]
    public async Task<ActionResult<FileSearchResult>> FileSearch([FromBody] RetrievalRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = GetTenantId(),
            Query = request.Query,
            TopK = request.TopK,
            Filters = request.Filters
        }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("web-search")]
    public async Task<ActionResult<WebSearchResponseDtoV2>> WebSearch([FromBody] WebSearchRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var result = await _webSearchTool.SearchAsync(new WebSearchRequest
        {
            TenantId = GetTenantId(),
            Query = request.Query,
            TopK = request.TopK
        }, cancellationToken);

        return Ok(new WebSearchResponseDtoV2
        {
            Hits = result.Hits.Select(hit => new WebSearchHitDtoV2
            {
                Title = hit.Title,
                Url = hit.Url,
                Snippet = hit.Snippet,
                Score = hit.Score
            }).ToList()
        });
    }

    [HttpPost("code-interpreter")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<CodeInterpreterResponseDtoV2>> RunCode([FromBody] CodeInterpreterRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var result = await _codeInterpreter.ExecuteAsync(new CodeInterpreterRequest
        {
            TenantId = GetTenantId(),
            Language = request.Language,
            Code = request.Code,
            InputArtifacts = request.InputArtifacts
        }, cancellationToken);

        return Ok(new CodeInterpreterResponseDtoV2
        {
            ExitCode = result.ExitCode,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
            OutputArtifacts = result.OutputArtifacts.ToList()
        });
    }

    [HttpPost("agents/run")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<AgentRunResponseDtoV2>> RunAgent([FromBody] AgentRunRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var result = await _agentRuntime.RunAsync(new AgentRunRequest
        {
            TenantId = GetTenantId(),
            AgentName = request.AgentName,
            Objective = request.Objective,
            ToolBudget = request.ToolBudget,
            Input = request.Input
        }, cancellationToken);

        return Ok(new AgentRunResponseDtoV2
        {
            AgentRunId = result.AgentRunId,
            Status = result.Status,
            Output = result.Output
        });
    }

    [HttpGet("operational-audit")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<OperationalAuditFeedResponseDto>> GetOperationalAudit(
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _operationalAuditStore.ReadAuditFeedAsync(new OperationalAuditFeedQuery
        {
            TenantId = GetTenantId(),
            Category = category,
            Status = status,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Cursor = cursor,
            Limit = limit
        }, cancellationToken);

        return Ok(new OperationalAuditFeedResponseDto
        {
            Entries = result.Entries.Select(entry => new OperationalAuditEntryDto
            {
                EntryId = entry.EntryId,
                Category = entry.Category,
                Status = entry.Status,
                Title = entry.Title,
                Summary = entry.Summary,
                DetailsJson = entry.DetailsJson,
                CreatedAtUtc = entry.CreatedAtUtc,
                CompletedAtUtc = entry.CompletedAtUtc
            }).ToList(),
            NextCursor = result.NextCursor
        });
    }

    private Guid GetTenantId()
    {
        var tenantIdRaw = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantIdRaw, out var tenantId)
            ? tenantId
            : Guid.Empty;
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}