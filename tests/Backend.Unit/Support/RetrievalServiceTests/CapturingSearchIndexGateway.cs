using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class CapturingSearchIndexGateway : ISearchIndexGateway
{
    public FileSearchFilterDto? LastFilters { get; private set; }

    public float[]? LastQueryEmbedding { get; private set; }

    public List<SearchResultDto> Results { get; set; } = new();

    public List<DocumentChunkIndexDto> DocumentChunks { get; set; } = new();

    public int CallCount { get; private set; }

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        return Task.FromResult(DocumentChunks.ToList());
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        LastFilters = filters;
        LastQueryEmbedding = queryEmbedding;
        CallCount++;
        return Task.FromResult(Results.Take(topK).ToList());
    }

    public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
