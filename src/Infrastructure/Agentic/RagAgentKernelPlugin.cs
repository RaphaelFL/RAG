using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace Chatbot.Infrastructure.Agentic;

internal sealed class RagAgentKernelPlugin
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFileSearchTool _fileSearchTool;
    private readonly IWebSearchTool _webSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IPromptAssembler _promptAssembler;

    public RagAgentKernelPlugin(
        IFileSearchTool fileSearchTool,
        IWebSearchTool webSearchTool,
        ICodeInterpreter codeInterpreter,
        IPromptAssembler promptAssembler)
    {
        _fileSearchTool = fileSearchTool;
        _webSearchTool = webSearchTool;
        _codeInterpreter = codeInterpreter;
        _promptAssembler = promptAssembler;
    }

    [KernelFunction("file_search")]
    public async Task<string> FileSearchAsync(string tenantId, string query, string topK = "5", string? filtersJson = null)
    {
        var resolvedTenantId = Guid.TryParse(tenantId, out var parsedTenantId) ? parsedTenantId : Guid.Empty;
        var filters = JsonSerializer.Deserialize<Dictionary<string, string[]>>(filtersJson ?? "{}", SerializerOptions)
            ?? new Dictionary<string, string[]>();

        var result = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = resolvedTenantId,
            Query = query,
            TopK = int.TryParse(topK, out var parsedTopK) ? Math.Max(1, parsedTopK) : 5,
            Filters = filters
        }, CancellationToken.None);

        return JsonSerializer.Serialize(new
        {
            matches = result.Matches.Select(match => new
            {
                match.ChunkId,
                match.DocumentId,
                match.Score,
                match.Text,
                Metadata = match.Metadata
            })
        }, SerializerOptions);
    }

    [KernelFunction("web_search")]
    public async Task<string> WebSearchAsync(string tenantId, string query, string topK = "5")
    {
        var resolvedTenantId = Guid.TryParse(tenantId, out var parsedTenantId) ? parsedTenantId : Guid.Empty;
        var result = await _webSearchTool.SearchAsync(new WebSearchRequest
        {
            TenantId = resolvedTenantId,
            Query = query,
            TopK = int.TryParse(topK, out var parsedTopK) ? Math.Max(1, parsedTopK) : 5
        }, CancellationToken.None);

        return JsonSerializer.Serialize(new { hits = result.Hits }, SerializerOptions);
    }

    [KernelFunction("code_interpreter")]
    public async Task<string> CodeInterpreterAsync(string tenantId, string code, string language = "python")
    {
        var resolvedTenantId = Guid.TryParse(tenantId, out var parsedTenantId) ? parsedTenantId : Guid.Empty;
        var result = await _codeInterpreter.ExecuteAsync(new CodeInterpreterRequest
        {
            TenantId = resolvedTenantId,
            Code = code,
            Language = language
        }, CancellationToken.None);

        return JsonSerializer.Serialize(result, SerializerOptions);
    }

    [KernelFunction("assemble_prompt")]
    public async Task<string> AssemblePromptAsync(
        string tenantId,
        string question,
        string systemInstructions,
        string? contextJson = null,
        string maxPromptTokens = "4000",
        string allowGeneralKnowledge = "false")
    {
        var resolvedTenantId = Guid.TryParse(tenantId, out var parsedTenantId) ? parsedTenantId : Guid.Empty;
        var chunks = DeserializeChunks(contextJson);
        var result = await _promptAssembler.AssembleAsync(new PromptAssemblyRequest
        {
            TenantId = resolvedTenantId,
            UserQuestion = question,
            SystemInstructions = systemInstructions,
            Chunks = chunks,
            MaxPromptTokens = int.TryParse(maxPromptTokens, out var parsedMaxPromptTokens) ? Math.Max(256, parsedMaxPromptTokens) : 4000,
            AllowGeneralKnowledge = bool.TryParse(allowGeneralKnowledge, out var parsedAllowGeneralKnowledge) && parsedAllowGeneralKnowledge
        }, CancellationToken.None);

        return JsonSerializer.Serialize(new
        {
            prompt = result.Prompt,
            includedChunkIds = result.IncludedChunkIds,
            estimatedPromptTokens = result.EstimatedPromptTokens,
            citations = result.HumanReadableCitations
        }, SerializerOptions);
    }

    private static RetrievedChunk[] DeserializeChunks(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return Array.Empty<RetrievedChunk>();
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<FileSearchEnvelope>(contextJson, SerializerOptions);
            if (envelope?.Matches is { Count: > 0 })
            {
                return envelope.Matches.Select(match => new RetrievedChunk
                {
                    ChunkId = match.ChunkId ?? string.Empty,
                    DocumentId = match.DocumentId,
                    Score = match.Score,
                    Text = match.Text ?? string.Empty,
                    Metadata = match.Metadata ?? new Dictionary<string, string>()
                }).ToArray();
            }

            var directMatches = JsonSerializer.Deserialize<List<FileSearchMatch>>(contextJson, SerializerOptions);
            return directMatches?.Select(match => new RetrievedChunk
            {
                ChunkId = match.ChunkId ?? string.Empty,
                DocumentId = match.DocumentId,
                Score = match.Score,
                Text = match.Text ?? string.Empty,
                Metadata = match.Metadata ?? new Dictionary<string, string>()
            }).ToArray() ?? Array.Empty<RetrievedChunk>();
        }
        catch
        {
            return Array.Empty<RetrievedChunk>();
        }
    }
}
