using System.Security.Claims;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Mcp;

public sealed class McpServer : IMcpServer
{
    private readonly IRetrievalService _retrievalService;
    private readonly IIngestionPipeline _ingestionPipeline;
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly FeatureFlagOptions _featureFlags;

    public McpServer(
        IRetrievalService retrievalService,
        IIngestionPipeline ingestionPipeline,
        IPromptTemplateRegistry promptTemplateRegistry,
        IOptions<FeatureFlagOptions> featureFlags)
    {
        _retrievalService = retrievalService;
        _ingestionPipeline = ingestionPipeline;
        _promptTemplateRegistry = promptTemplateRegistry;
        _featureFlags = featureFlags.Value;
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
                        new { name = "list_templates", description = "Lista templates versionados disponiveis." }
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
                        new { uri = "rag://retrieval-policies", name = "Retrieval policies" }
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
                states = new[] { "Uploaded", "Queued", "Parsing", "OcrProcessing", "Chunking", "Indexed", "ReindexPending", "Failed", "Archived", "Deleted" }
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

        var result = await _retrievalService.QueryAsync(new SearchQueryRequestDto
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

        var args = GetArguments(root);
        var mode = args.TryGetProperty("mode", out var modeProperty) ? modeProperty.GetString() ?? "incremental" : "incremental";
        var documentIds = args.TryGetProperty("documentIds", out var idsProperty)
            ? idsProperty.EnumerateArray().Select(item => item.GetGuid()).ToList()
            : new List<Guid>();

        var result = await _ingestionPipeline.ReindexAsync(new BulkReindexRequestDto
        {
            DocumentIds = documentIds,
            Mode = mode
        }, cancellationToken);

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
