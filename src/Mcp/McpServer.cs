using System.Security.Claims;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;
using InfraCfg = Chatbot.Infrastructure.Configuration;

namespace Chatbot.Mcp;

public sealed class McpServer : IMcpServer
{
    private readonly IRetrievalService _retrievalService;
    private readonly ISearchQueryService _searchQueryService;
    private readonly IDocumentReindexService _documentReindexService;
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly InfraCfg.FeatureFlagOptions _featureFlags;
    private readonly IFileSearchTool _fileSearchTool;
    private readonly IWebSearchTool _webSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IAgentRuntime _agentRuntime;
    private readonly IPromptAssembler _promptAssembler;
    private readonly IEmbeddingGenerationService _embeddingGenerationService;
    private readonly AppCfg.EmbeddingGenerationOptions _embeddingOptions;
    private readonly AppCfg.VectorStoreOptions _vectorStoreOptions;
    private readonly AppCfg.AgentRuntimeOptions _agentRuntimeOptions;
    private readonly AppCfg.WebSearchOptions _webSearchOptions;
    private readonly AppCfg.CodeInterpreterOptions _codeInterpreterOptions;

    public McpServer(
        IRetrievalService retrievalService,
        ISearchQueryService searchQueryService,
        IDocumentReindexService documentReindexService,
        IPromptTemplateRegistry promptTemplateRegistry,
        IFileSearchTool fileSearchTool,
        IWebSearchTool webSearchTool,
        ICodeInterpreter codeInterpreter,
        IAgentRuntime agentRuntime,
        IPromptAssembler promptAssembler,
        IEmbeddingGenerationService embeddingGenerationService,
        IOptions<InfraCfg.FeatureFlagOptions> featureFlags,
        IOptions<AppCfg.EmbeddingGenerationOptions> embeddingOptions,
        IOptions<AppCfg.VectorStoreOptions> vectorStoreOptions,
        IOptions<AppCfg.AgentRuntimeOptions> agentRuntimeOptions,
        IOptions<AppCfg.WebSearchOptions> webSearchOptions,
        IOptions<AppCfg.CodeInterpreterOptions> codeInterpreterOptions)
    {
        _retrievalService = retrievalService;
        _searchQueryService = searchQueryService;
        _documentReindexService = documentReindexService;
        _promptTemplateRegistry = promptTemplateRegistry;
        _featureFlags = featureFlags.Value;
        _fileSearchTool = fileSearchTool;
        _webSearchTool = webSearchTool;
        _codeInterpreter = codeInterpreter;
        _agentRuntime = agentRuntime;
        _promptAssembler = promptAssembler;
        _embeddingGenerationService = embeddingGenerationService;
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
            return Error(request.Id, -32601, "MCP is disabled.");
        }

        if (!string.Equals(request.Jsonrpc, "2.0", StringComparison.Ordinal))
        {
            return Error(request.Id, -32600, "Invalid JSON-RPC version.");
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
            _ => Error(request.Id, -32601, "Method not found.")
        };
    }

    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request.Params));
        var root = document.RootElement;
        var toolName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

        return toolName switch
        {
            "search" => await CallSearchAsync(request.Id, root, cancellationToken),
            "search_knowledge" => await CallSearchAsync(request.Id, root, cancellationToken),
            "retrieve_document_chunks" => await CallRetrieveChunksAsync(request.Id, root, cancellationToken),
            "summarize_sources" => await CallSummarizeSourcesAsync(request.Id, root, cancellationToken),
            "reindex" => await CallReindexAsync(request.Id, root, user, cancellationToken),
            "reindex_document" => await CallReindexAsync(request.Id, root, user, cancellationToken),
            "list_templates" => CallListTemplates(request.Id),
            "file_search" => await CallFileSearchAsync(request.Id, root, user, cancellationToken),
            "assemble_prompt" => await CallPromptAssemblyAsync(request.Id, root, user, cancellationToken),
            "embedding_generate" => await CallEmbeddingGenerateAsync(request.Id, root, user, cancellationToken),
            "web_search" => await CallWebSearchAsync(request.Id, root, user, cancellationToken),
            "code_interpreter" => await CallCodeInterpreterAsync(request.Id, root, user, cancellationToken),
            "agent_run" => await CallAgentRunAsync(request.Id, root, user, cancellationToken),
            _ => Error(request.Id, -32602, "Unknown tool.")
        };
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
            ? Error(request.Id, -32602, "Unknown resource.")
            : new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
    }

    private async Task<JsonRpcResponse> CallSearchAsync(string? id, JsonElement root, CancellationToken cancellationToken)
    {
        var arguments = GetArguments(root);
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var top = arguments.TryGetProperty("top", out var topElement) && topElement.TryGetInt32(out var parsedTop)
            ? parsedTop
            : 5;

        var result = await _searchQueryService.QueryAsync(new SearchQueryRequestDto
        {
            Query = query,
            Top = top
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    private async Task<JsonRpcResponse> CallRetrieveChunksAsync(string? id, JsonElement root, CancellationToken cancellationToken)
    {
        var arguments = GetArguments(root);
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var top = arguments.TryGetProperty("top", out var topElement) && topElement.TryGetInt32(out var parsedTop)
            ? parsedTop
            : 5;

        var result = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = query,
            TopK = top,
            SemanticRanking = true
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = new
            {
                chunks = result.Chunks,
                strategy = result.RetrievalStrategy,
                latencyMs = result.LatencyMs
            }
        };
    }

    private async Task<JsonRpcResponse> CallSummarizeSourcesAsync(string? id, JsonElement root, CancellationToken cancellationToken)
    {
        var arguments = GetArguments(root);
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var retrieval = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = query,
            TopK = 5,
            SemanticRanking = true
        }, cancellationToken);

        var distinctTitles = retrieval.Chunks
            .Select(chunk => chunk.DocumentTitle)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new JsonRpcResponse
        {
            Id = id,
            Result = new
            {
                sourceCount = retrieval.Chunks.Count,
                documents = distinctTitles,
                summary = retrieval.Chunks.Count == 0
                    ? "Nenhuma evidencia encontrada para resumir fontes."
                    : $"Fontes encontradas em {distinctTitles.Count} documento(s): {string.Join(", ", distinctTitles.Take(3))}."
            }
        };
    }

    private async Task<JsonRpcResponse> CallReindexAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (role is not ("Analyst" or "TenantAdmin" or "PlatformAdmin"))
        {
            return Error(id, -32003, "Administrative scope is required.");
        }

        var tenantClaim = user.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var args = GetArguments(root);
        var mode = args.TryGetProperty("mode", out var modeProperty) ? modeProperty.GetString() ?? "incremental" : "incremental";
        var documentIds = args.TryGetProperty("documentIds", out var idsProperty)
            ? idsProperty.EnumerateArray().Select(item => item.GetGuid()).ToList()
            : new List<Guid>();
        var includeAllTenantDocuments = args.TryGetProperty("includeAllTenantDocuments", out var includeAllProperty)
            ? includeAllProperty.GetBoolean()
            : documentIds.Count == 0;

        var result = await _documentReindexService.ReindexAsync(new BulkReindexRequestDto
        {
            DocumentIds = documentIds,
            IncludeAllTenantDocuments = includeAllTenantDocuments,
            Mode = mode
        }, tenantId, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    private JsonRpcResponse CallListTemplates(string? id)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Result = new
            {
                templates = _promptTemplateRegistry.ListAll().Select(template => new
                {
                    templateId = template.TemplateId,
                    version = template.Version
                })
            }
        };
    }

    private async Task<JsonRpcResponse> CallFileSearchAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdOrThrow(user, id);
        if (tenantId is null)
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var arguments = GetArguments(root);
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var top = arguments.TryGetProperty("top", out var topElement) && topElement.TryGetInt32(out var parsedTop)
            ? parsedTop
            : 5;

        var result = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = tenantId.Value,
            Query = query,
            TopK = top,
            Filters = new Dictionary<string, string[]>()
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = new { matches = result.Matches }
        };
    }

    private async Task<JsonRpcResponse> CallPromptAssemblyAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdOrThrow(user, id);
        if (tenantId is null)
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var arguments = GetArguments(root);
        var question = arguments.GetProperty("question").GetString() ?? string.Empty;
        var retrieval = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = tenantId.Value,
            Query = question,
            TopK = 5,
            Filters = new Dictionary<string, string[]>()
        }, cancellationToken);

        var prompt = await _promptAssembler.AssembleAsync(new PromptAssemblyRequest
        {
            TenantId = tenantId.Value,
            SystemInstructions = "Monte um prompt grounded, seguro e auditavel.",
            UserQuestion = question,
            Chunks = retrieval.Matches,
            MaxPromptTokens = 4000,
            AllowGeneralKnowledge = false
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = prompt
        };
    }

    private async Task<JsonRpcResponse> CallEmbeddingGenerateAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdOrThrow(user, id);
        if (tenantId is null)
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var arguments = GetArguments(root);
        var text = arguments.GetProperty("text").GetString() ?? string.Empty;
        var chunkId = arguments.TryGetProperty("chunkId", out var chunkIdElement) ? chunkIdElement.GetString() ?? "ad-hoc" : "ad-hoc";
        var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

        var result = await _embeddingGenerationService.GenerateBatchAsync(new EmbeddingBatchRequest
        {
            Inputs = new[]
            {
                new EmbeddingInput
                {
                    ChunkId = chunkId,
                    DocumentId = Guid.Empty,
                    TenantId = tenantId.Value,
                    ContentHash = contentHash,
                    Text = text
                }
            }
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    private async Task<JsonRpcResponse> CallWebSearchAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdOrThrow(user, id);
        if (tenantId is null)
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var arguments = GetArguments(root);
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var top = arguments.TryGetProperty("top", out var topElement) && topElement.TryGetInt32(out var parsedTop)
            ? parsedTop
            : 5;

        var result = await _webSearchTool.SearchAsync(new WebSearchRequest
        {
            TenantId = tenantId.Value,
            Query = query,
            TopK = top
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    private async Task<JsonRpcResponse> CallCodeInterpreterAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdOrThrow(user, id);
        if (tenantId is null)
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var arguments = GetArguments(root);
        var code = arguments.GetProperty("code").GetString() ?? string.Empty;
        var language = arguments.TryGetProperty("language", out var languageElement) ? languageElement.GetString() ?? "python" : "python";
        var result = await _codeInterpreter.ExecuteAsync(new CodeInterpreterRequest
        {
            TenantId = tenantId.Value,
            Code = code,
            Language = language
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    private async Task<JsonRpcResponse> CallAgentRunAsync(string? id, JsonElement root, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdOrThrow(user, id);
        if (tenantId is null)
        {
            return Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var arguments = GetArguments(root);
        var agentName = arguments.GetProperty("agentName").GetString() ?? string.Empty;
        var objective = arguments.TryGetProperty("objective", out var objectiveElement) ? objectiveElement.GetString() ?? string.Empty : string.Empty;
        var toolBudget = arguments.TryGetProperty("toolBudget", out var budgetElement) && budgetElement.TryGetInt32(out var parsedBudget)
            ? parsedBudget
            : 3;

        var result = await _agentRuntime.RunAsync(new AgentRunRequest
        {
            TenantId = tenantId.Value,
            AgentName = agentName,
            Objective = objective,
            ToolBudget = toolBudget,
            Input = new Dictionary<string, object?>
            {
                ["query"] = objective,
                ["question"] = objective
            }
        }, cancellationToken);

        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    private static Guid? GetTenantIdOrThrow(ClaimsPrincipal user, string? id)
    {
        var tenantClaim = user.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }

    private static JsonElement GetArguments(JsonElement root)
    {
        return root.TryGetProperty("arguments", out var arguments)
            ? arguments
            : root;
    }

    private static JsonRpcResponse Error(string? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };
    }
}
