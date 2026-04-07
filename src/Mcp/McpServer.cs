using System.Security.Claims;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;
using InfraCfg = Chatbot.Infrastructure.Configuration;

namespace Chatbot.Mcp;

public sealed class McpServer : IMcpServer
{
    private readonly IReadOnlyDictionary<string, IMcpToolHandler> _toolHandlers;
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly InfraCfg.FeatureFlagOptions _featureFlags;
    private readonly AppCfg.EmbeddingGenerationOptions _embeddingOptions;
    private readonly AppCfg.VectorStoreOptions _vectorStoreOptions;
    private readonly AppCfg.AgentRuntimeOptions _agentRuntimeOptions;
    private readonly AppCfg.WebSearchOptions _webSearchOptions;
    private readonly AppCfg.CodeInterpreterOptions _codeInterpreterOptions;

    public McpServer(
        IEnumerable<IMcpToolHandler> toolHandlers,
        IPromptTemplateRegistry promptTemplateRegistry,
        IOptions<InfraCfg.FeatureFlagOptions> featureFlags,
        IOptions<AppCfg.EmbeddingGenerationOptions> embeddingOptions,
        IOptions<AppCfg.VectorStoreOptions> vectorStoreOptions,
        IOptions<AppCfg.AgentRuntimeOptions> agentRuntimeOptions,
        IOptions<AppCfg.WebSearchOptions> webSearchOptions,
        IOptions<AppCfg.CodeInterpreterOptions> codeInterpreterOptions)
    {
        _toolHandlers = toolHandlers
            .SelectMany(handler => handler.SupportedToolNames.Select(toolName => new KeyValuePair<string, IMcpToolHandler>(toolName, handler)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _promptTemplateRegistry = promptTemplateRegistry;
        _featureFlags = featureFlags.Value;
        _embeddingOptions = embeddingOptions.Value;
        _vectorStoreOptions = vectorStoreOptions.Value;
        _agentRuntimeOptions = agentRuntimeOptions.Value;
        _webSearchOptions = webSearchOptions.Value;
        _codeInterpreterOptions = codeInterpreterOptions.Value;
    }

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!_featureFlags.EnableMcp)
        {
            return McpResponseFactory.Error(request.Id, -32601, "MCP is disabled.");
        }

        if (!string.Equals(request.Jsonrpc, "2.0", StringComparison.Ordinal))
        {
            return McpResponseFactory.Error(request.Id, -32600, "Invalid JSON-RPC version.");
        }

        return request.Method switch
        {
            "tools/list" => new JsonRpcResponse
            {
                Id = request.Id,
                Result = new
                {
                    tools = new[]
                    {
                        new { name = "search", description = "Alias legado para search_knowledge." },
                        new { name = "search_knowledge", description = "Busca conhecimento indexado por tenant." },
                        new { name = "retrieve_document_chunks", description = "Recupera chunks relevantes para uma consulta." },
                        new { name = "summarize_sources", description = "Resume as fontes recuperadas para uma consulta." },
                        new { name = "reindex", description = "Alias legado para reindex_document." },
                        new { name = "reindex_document", description = "Reindexa documentos existentes. Requer papel administrativo." },
                        new { name = "list_templates", description = "Lista templates versionados disponiveis." },
                        new { name = "file_search", description = "Busca agentic em arquivos internos usando retrieval governado." },
                        new { name = "assemble_prompt", description = "Monta prompt final grounded com contexto e citacoes." },
                        new { name = "embedding_generate", description = "Gera embeddings pela capacidade interna da plataforma." },
                        new { name = "web_search", description = "Executa busca web pela tool configurada." },
                        new { name = "code_interpreter", description = "Executa codigo em interpretador controlado." },
                        new { name = "agent_run", description = "Executa um agent governado com budget e timeout." }
                    }
                }
            },
            "resources/list" => new JsonRpcResponse
            {
                Id = request.Id,
                Result = new
                {
                    resources = new[]
                    {
                        new { uri = "rag://knowledge-base-schema", name = "Knowledge base schema" },
                        new { uri = "rag://prompt-catalog", name = "Prompt catalog" },
                        new { uri = "rag://retrieval-policies", name = "Retrieval policies" },
                        new { uri = "rag://embedding-model-info", name = "Embedding model info" },
                        new { uri = "rag://runtime-capabilities", name = "Runtime capabilities" },
                        new { uri = "rag://vector-store-stats", name = "Vector store stats" },
                        new { uri = "rag://prompt-assembly-policy", name = "Prompt assembly policy" }
                    }
                }
            },
            "resources/read" => HandleResourceRead(request),
            "prompts/list" => new JsonRpcResponse
            {
                Id = request.Id,
                Result = new
                {
                    prompts = _promptTemplateRegistry.ListAll().Select(template => new
                    {
                        name = template.TemplateId,
                        version = template.Version
                    })
                }
            },
            "tools/call" => await HandleToolCallAsync(request, user, cancellationToken),
            _ => McpResponseFactory.Error(request.Id, -32601, "Method not found.")
        };
    }

    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request.Params));
        var root = document.RootElement;
        var toolName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return McpResponseFactory.Error(request.Id, -32602, "Unknown tool.");
        }

        if (!_toolHandlers.TryGetValue(toolName, out var handler))
        {
            return McpResponseFactory.Error(request.Id, -32602, "Unknown tool.");
        }

        return await handler.HandleAsync(request.Id, toolName, McpRequestHelpers.GetArguments(root), user, cancellationToken);
    }

    private JsonRpcResponse HandleResourceRead(JsonRpcRequest request)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request.Params));
        var root = document.RootElement;
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

        return result is null
            ? McpResponseFactory.Error(request.Id, -32602, "Unknown resource.")
            : McpResponseFactory.Ok(request.Id, result);
    }
}
