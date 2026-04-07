using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.RagPersistenceTestsSupport;

internal sealed class CapturingPersistentChunkGateway : ISearchIndexGateway
{
    private readonly Guid _documentId;

    public CapturingPersistentChunkGateway(Guid documentId)
    {
        _documentId = documentId;
    }

    public List<DocumentChunkIndexDto> ReindexedChunks { get; } = new();

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        if (documentId != _documentId)
        {
            return Task.FromResult(new List<DocumentChunkIndexDto>());
        }

        return Task.FromResult(new List<DocumentChunkIndexDto>
        {
            new()
            {
                ChunkId = "chunk-003",
                DocumentId = _documentId,
                Content = "Chunk recuperado do indice persistido",
                Embedding = new[] { 0.7f, 0.8f, 0.9f },
                PageNumber = 3,
                Section = "Operacao",
                Metadata = new Dictionary<string, string>
                {
                    ["chunkIndex"] = "0",
                    ["contentHash"] = "hash-atual",
                    ["embeddingModel"] = "default"
                }
            }
        });
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        return Task.FromResult(new List<SearchResultDto>());
    }

    public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        ReindexedChunks.AddRange(chunks.Select(chunk => new DocumentChunkIndexDto
        {
            ChunkId = chunk.ChunkId,
            DocumentId = chunk.DocumentId,
            Content = chunk.Content,
            Embedding = chunk.Embedding?.ToArray(),
            PageNumber = chunk.PageNumber,
            Section = chunk.Section,
            Metadata = new Dictionary<string, string>(chunk.Metadata)
        }));
        return Task.CompletedTask;
    }
}
