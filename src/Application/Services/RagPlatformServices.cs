using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class CurrentStateEmbeddingGenerationService : IEmbeddingGenerationService
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly EmbeddingGenerationOptions _options;

    public CurrentStateEmbeddingGenerationService(
        IEmbeddingProvider embeddingProvider,
        IOptions<EmbeddingGenerationOptions> options)
    {
        _embeddingProvider = embeddingProvider;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<EmbeddingEnvelope>> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken ct)
    {
        var items = request.Inputs
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .GroupBy(item => item.ContentHash, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var generated = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            generated[item.ContentHash] = (await _embeddingProvider.CreateEmbeddingAsync(item.Text, null, ct)).ToArray();
        }

        return request.Inputs.Select(item => new EmbeddingEnvelope
        {
            ChunkId = item.ChunkId,
            EmbeddingModelName = string.IsNullOrWhiteSpace(request.EmbeddingModelName) ? _options.ModelName : request.EmbeddingModelName,
            EmbeddingModelVersion = string.IsNullOrWhiteSpace(request.EmbeddingModelVersion) ? _options.ModelVersion : request.EmbeddingModelVersion,
            VectorDimensions = generated.TryGetValue(item.ContentHash, out var vector) ? vector.Length : _options.Dimensions,
            Vector = generated.TryGetValue(item.ContentHash, out var resolvedVector) ? resolvedVector : Array.Empty<float>()
        }).ToArray();
    }
}

public sealed class CurrentStateRetrieverAdapter : IRetriever
{
    private readonly IRetrievalService _retrievalService;

    public CurrentStateRetrieverAdapter(IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public async Task<RetrievedContext> RetrieveAsync(RetrievalPlan request, CancellationToken ct)
    {
        var result = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = request.QueryText,
            TopK = request.TopK,
            DocumentIds = ReadGuidFilter(request.Filters, "documentIds"),
            Tags = ReadStringFilter(request.Filters, "tags"),
            Categories = ReadStringFilter(request.Filters, "categories"),
            ContentTypes = ReadStringFilter(request.Filters, "contentTypes"),
            Sources = ReadStringFilter(request.Filters, "sources"),
            SemanticRanking = request.UseHybridRetrieval || request.UseReranking
        }, ct);

        return new RetrievedContext
        {
            RetrievalStrategy = result.RetrievalStrategy,
            LatencyMs = result.LatencyMs,
            Chunks = result.Chunks.Select(chunk => new RetrievedChunk
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Score = chunk.Score,
                Text = chunk.Content,
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["documentTitle"] = chunk.DocumentTitle,
                    ["section"] = chunk.Section ?? string.Empty,
                    ["page"] = chunk.PageNumber.ToString(),
                    ["endPage"] = chunk.EndPageNumber.ToString()
                }
            }).ToArray()
        };
    }

    private static List<Guid>? ReadGuidFilter(Dictionary<string, string[]> filters, string key)
    {
        if (!filters.TryGetValue(key, out var values) || values.Length == 0)
        {
            return null;
        }

        return values
            .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToList();
    }

    private static List<string>? ReadStringFilter(Dictionary<string, string[]> filters, string key)
    {
        if (!filters.TryGetValue(key, out var values) || values.Length == 0)
        {
            return null;
        }

        return values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
    }
}

public sealed class CurrentStateRerankerAdapter : IReranker
{
    public Task<IReadOnlyCollection<RetrievedChunk>> RerankAsync(RerankRequest request, CancellationToken ct)
    {
        var ordered = request.Candidates
            .OrderByDescending(candidate => candidate.Score)
            .Take(request.TopK)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<RetrievedChunk>>(ordered);
    }
}

public sealed class CurrentStatePromptAssembler : IPromptAssembler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IOperationalAuditStore _operationalAuditStore;

    public CurrentStatePromptAssembler(IOperationalAuditStore operationalAuditStore)
    {
        _operationalAuditStore = operationalAuditStore;
    }

    public async Task<PromptAssemblyResult> AssembleAsync(PromptAssemblyRequest request, CancellationToken ct)
    {
        var selectedChunks = request.Chunks
            .GroupBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(chunk => chunk.Score)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine(request.SystemInstructions);
        builder.AppendLine();
        builder.AppendLine("Regras:");
        builder.AppendLine(request.AllowGeneralKnowledge
            ? "- Use o contexto recuperado como fonte principal; so complemente com conhecimento geral se necessario e deixe isso claro."
            : "- Responda apenas com base no contexto recuperado; se nao houver evidencia suficiente, diga explicitamente que faltou evidencia.");
        builder.AppendLine("- Nao inclua hashes, ids tecnicos opacos ou lixo operacional na resposta.");
        builder.AppendLine("- Cite origem humana legivel ao usar evidencias.");
        builder.AppendLine();
        builder.AppendLine("Contexto recuperado:");

        foreach (var chunk in selectedChunks)
        {
            var sourceName = chunk.Metadata.TryGetValue("documentTitle", out var title) && !string.IsNullOrWhiteSpace(title)
                ? title
                : chunk.DocumentId.ToString();
            var section = chunk.Metadata.TryGetValue("section", out var sectionValue) ? sectionValue : string.Empty;
            var page = chunk.Metadata.TryGetValue("page", out var pageValue) ? pageValue : string.Empty;
            builder.AppendLine($"[{chunk.ChunkId}] Fonte: {sourceName}; Secao: {section}; Pagina: {page}");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Pergunta do usuario:");
        builder.AppendLine(request.UserQuestion);

        var result = new PromptAssemblyResult
        {
            Prompt = builder.ToString(),
            IncludedChunkIds = selectedChunks.Select(chunk => chunk.ChunkId).ToArray(),
            EstimatedPromptTokens = Math.Max(1, builder.Length / 4),
            HumanReadableCitations = selectedChunks.Select(chunk =>
            {
                var sourceName = chunk.Metadata.TryGetValue("documentTitle", out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : chunk.DocumentId.ToString();
                return $"{sourceName} ({chunk.ChunkId})";
            }).ToArray()
        };

        await _operationalAuditStore.WritePromptAssemblyAsync(new PromptAssemblyRecord
        {
            PromptAssemblyId = Guid.NewGuid(),
            TenantId = request.TenantId,
            PromptTemplateId = request.AllowGeneralKnowledge ? "current-state-general" : "current-state-grounded",
            MaxPromptTokens = request.MaxPromptTokens,
            UsedPromptTokens = result.EstimatedPromptTokens,
            IncludedChunkIdsJson = JsonSerializer.Serialize(result.IncludedChunkIds, SerializerOptions),
            PromptBody = result.Prompt,
            CreatedAtUtc = DateTime.UtcNow
        }, ct);

        return result;
    }
}

public sealed class CurrentStateFileSearchTool : IFileSearchTool
{
    private readonly IRetriever _retriever;

    public CurrentStateFileSearchTool(IRetriever retriever)
    {
        _retriever = retriever;
    }

    public async Task<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken ct)
    {
        var retrieval = await _retriever.RetrieveAsync(new RetrievalPlan
        {
            TenantId = request.TenantId,
            QueryText = request.Query,
            Filters = request.Filters,
            TopK = request.TopK,
            MaxContextChunks = request.TopK,
            UseDenseRetrieval = true,
            UseHybridRetrieval = true,
            UseReranking = true
        }, ct);

        return new FileSearchResult
        {
            Matches = retrieval.Chunks
        };
    }
}

public sealed class DisabledWebSearchTool : IWebSearchTool
{
    public Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct)
    {
        return Task.FromResult(new WebSearchResult
        {
            Hits = Array.Empty<WebSearchHit>()
        });
    }
}

public sealed class DisabledCodeInterpreter : ICodeInterpreter
{
    public Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct)
    {
        return Task.FromResult(new CodeInterpreterResult
        {
            ExitCode = -1,
            StdErr = "Code interpreter desabilitado nesta configuracao.",
            StdOut = string.Empty,
            OutputArtifacts = Array.Empty<string>()
        });
    }
}

public sealed class GovernedAgentRuntime : IAgentRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFileSearchTool _fileSearchTool;
    private readonly IWebSearchTool _webSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IPromptAssembler _promptAssembler;
    private readonly AgentRuntimeOptions _options;
    private readonly IOperationalAuditStore _operationalAuditStore;

    public GovernedAgentRuntime(
        IFileSearchTool fileSearchTool,
        IWebSearchTool webSearchTool,
        ICodeInterpreter codeInterpreter,
        IPromptAssembler promptAssembler,
        IOptions<AgentRuntimeOptions> options,
        IOperationalAuditStore operationalAuditStore)
    {
        _fileSearchTool = fileSearchTool;
        _webSearchTool = webSearchTool;
        _codeInterpreter = codeInterpreter;
        _promptAssembler = promptAssembler;
        _options = options.Value;
        _operationalAuditStore = operationalAuditStore;
    }

    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken ct)
    {
        var agentRunId = Guid.NewGuid();
        var startedAtUtc = DateTime.UtcNow;
        var toolBudget = Math.Min(request.ToolBudget, _options.MaxToolBudget);
        var usedTools = 0;
        if (toolBudget <= 0)
        {
            var rejected = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "rejected",
                Output = new Dictionary<string, object?> { ["reason"] = "Tool budget invalido." }
            };

            await WriteAgentRunAsync(request, rejected, toolBudget, usedTools, startedAtUtc, ct);
            return rejected;
        }

        var result = new Dictionary<string, object?>();
        switch (request.AgentName)
        {
            case "FileSearchAgent":
            {
                var query = request.Input.TryGetValue("query", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new FileSearchRequest
                {
                    TenantId = request.TenantId,
                    Query = query,
                    TopK = 5,
                    Filters = new Dictionary<string, string[]>()
                };
                usedTools++;
                var matches = await _fileSearchTool.SearchAsync(toolRequest, ct);
                await _operationalAuditStore.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "file_search",
                    Status = "completed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(new { matches = matches.Matches.Select(match => new { match.ChunkId, match.DocumentId, match.Score }) }, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["matches"] = matches.Matches.Select(match => new { match.ChunkId, match.DocumentId, match.Score }).ToArray();
                break;
            }
            case "WebSearchAgent":
            {
                var query = request.Input.TryGetValue("query", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new WebSearchRequest { TenantId = request.TenantId, Query = query, TopK = 5 };
                usedTools++;
                var hits = await _webSearchTool.SearchAsync(toolRequest, ct);
                await _operationalAuditStore.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "web_search",
                    Status = "completed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(new { hits = hits.Hits }, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["hits"] = hits.Hits.ToArray();
                break;
            }
            case "CodeInterpreterAgent":
            {
                var code = request.Input.TryGetValue("code", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new CodeInterpreterRequest { TenantId = request.TenantId, Code = code };
                usedTools++;
                var execution = await _codeInterpreter.ExecuteAsync(toolRequest, ct);
                await _operationalAuditStore.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "code_interpreter",
                    Status = execution.ExitCode == 0 ? "completed" : "failed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(execution, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["exitCode"] = execution.ExitCode;
                result["stderr"] = execution.StdErr;
                break;
            }
            case "PromptAssemblyAgent":
            {
                var question = request.Input.TryGetValue("question", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new PromptAssemblyRequest
                {
                    TenantId = request.TenantId,
                    UserQuestion = question,
                    SystemInstructions = "Monte um prompt grounded, seguro e auditavel.",
                    Chunks = Array.Empty<RetrievedChunk>(),
                    MaxPromptTokens = 4000,
                    AllowGeneralKnowledge = false
                };
                usedTools++;
                var prompt = await _promptAssembler.AssembleAsync(toolRequest, ct);
                await _operationalAuditStore.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "assemble_prompt",
                    Status = "completed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(new { prompt.Prompt, prompt.EstimatedPromptTokens, prompt.IncludedChunkIds }, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["prompt"] = prompt.Prompt;
                break;
            }
            default:
                result["message"] = "Agent ainda nao implementado nesta etapa.";
                break;
        }

        var completed = new AgentRunResult
        {
            AgentRunId = agentRunId,
            Status = "completed",
            Output = result
        };

        await WriteAgentRunAsync(request, completed, toolBudget, usedTools, startedAtUtc, ct);
        return completed;
    }

    private Task WriteAgentRunAsync(AgentRunRequest request, AgentRunResult result, int toolBudget, int usedTools, DateTime startedAtUtc, CancellationToken ct)
    {
        return _operationalAuditStore.WriteAgentRunAsync(new AgentRunRecord
        {
            AgentRunId = result.AgentRunId,
            TenantId = request.TenantId,
            AgentName = request.AgentName,
            Status = result.Status,
            ToolBudget = toolBudget,
            RemainingBudget = Math.Max(0, toolBudget - usedTools),
            InputJson = JsonSerializer.Serialize(new { request.Objective, request.Input }, SerializerOptions),
            OutputJson = JsonSerializer.Serialize(result.Output, SerializerOptions),
            CreatedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);
    }
}