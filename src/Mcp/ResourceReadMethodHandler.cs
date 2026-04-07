using System.Security.Claims;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;
using InfraCfg = Chatbot.Infrastructure.Configuration;

namespace Chatbot.Mcp;

public sealed class ResourceReadMethodHandler : IMcpMethodHandler
{
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly InfraCfg.FeatureFlagOptions _featureFlags;
    private readonly AppCfg.EmbeddingGenerationOptions _embeddingOptions;
    private readonly AppCfg.VectorStoreOptions _vectorStoreOptions;
    private readonly AppCfg.AgentRuntimeOptions _agentRuntimeOptions;
    private readonly AppCfg.WebSearchOptions _webSearchOptions;
    private readonly AppCfg.CodeInterpreterOptions _codeInterpreterOptions;

    public ResourceReadMethodHandler(
        IPromptTemplateRegistry promptTemplateRegistry,
        IOptions<InfraCfg.FeatureFlagOptions> featureFlags,
        IOptions<AppCfg.EmbeddingGenerationOptions> embeddingOptions,
        IOptions<AppCfg.VectorStoreOptions> vectorStoreOptions,
        IOptions<AppCfg.AgentRuntimeOptions> agentRuntimeOptions,
        IOptions<AppCfg.WebSearchOptions> webSearchOptions,
        IOptions<AppCfg.CodeInterpreterOptions> codeInterpreterOptions)
    {
        _promptTemplateRegistry = promptTemplateRegistry;
        _featureFlags = featureFlags.Value;
        _embeddingOptions = embeddingOptions.Value;
        _vectorStoreOptions = vectorStoreOptions.Value;
        _agentRuntimeOptions = agentRuntimeOptions.Value;
        _webSearchOptions = webSearchOptions.Value;
        _codeInterpreterOptions = codeInterpreterOptions.Value;
    }

    public IReadOnlyCollection<string> SupportedMethods { get; } = new[] { "resources/read" };

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var root = McpRequestHelpers.GetRoot(request.Params);
        var uri = root.TryGetProperty("uri", out var uriElement) ? uriElement.GetString() : null;

        object? result = uri switch
        {
            "rag://knowledge-base-schema" => new
            {
                entities = new[] { "document", "chunk", "citation", "template" },
                states = new[] { "Uploaded", "Queued", "Parsing", "OcrProcessing", "Chunking", "Embedding", "Indexing", "Indexed", "ReindexPending", "Failed", "Archived", "Deleted" }
            },
            "rag://prompt-catalog" => _promptTemplateRegistry.ListAll().Select(template => new
            {
                templateId = template.TemplateId,
                version = template.Version
            }).ToArray(),
            "rag://retrieval-policies" => new
            {
                strategy = "hybrid",
                tenantIsolation = true,
                citationsRequiredWhenGrounded = true
            },
            "rag://embedding-model-info" => new
            {
                modelName = _embeddingOptions.ModelName,
                modelVersion = _embeddingOptions.ModelVersion,
                dimensions = _embeddingOptions.Dimensions,
                runtime = _embeddingOptions.PrimaryRuntime
            },
            "rag://runtime-capabilities" => new
            {
                mcpEnabled = _featureFlags.EnableMcp,
                webSearchEnabled = _webSearchOptions.Enabled,
                codeInterpreterEnabled = _codeInterpreterOptions.Enabled,
                agentRuntimeEnabled = _agentRuntimeOptions.Enabled,
                semanticRankingEnabled = _featureFlags.EnableSemanticRanking,
                graphRagEnabled = _featureFlags.EnableGraphRag
            },
            "rag://vector-store-stats" => new
            {
                provider = _vectorStoreOptions.Provider,
                defaultTopK = _vectorStoreOptions.DefaultTopK,
                scoreThreshold = _vectorStoreOptions.DefaultScoreThreshold,
                schema = _vectorStoreOptions.Schema
            },
            "rag://prompt-assembly-policy" => new
            {
                removesOpaqueIdentifiers = true,
                removesTechnicalNoise = true,
                compressesRedundantContext = true,
                includesHumanReadableCitations = true
            },
            _ => null
        };

        return Task.FromResult(result is null
            ? McpResponseFactory.Error(request.Id, -32602, "Unknown resource.")
            : McpResponseFactory.Ok(request.Id, result));
    }
}