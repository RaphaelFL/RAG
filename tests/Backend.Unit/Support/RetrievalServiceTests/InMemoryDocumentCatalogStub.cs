using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class InMemoryDocumentCatalogStub : IDocumentCatalog
{
    private readonly Dictionary<Guid, DocumentCatalogEntry> _entries = new();

    public void Upsert(DocumentCatalogEntry entry) => _entries[entry.DocumentId] = entry;

    public DocumentCatalogEntry? Get(Guid documentId) => _entries.TryGetValue(documentId, out var entry) ? entry : null;

    public IReadOnlyCollection<DocumentCatalogEntry> Query(FileSearchFilterDto? filters) => _entries.Values.ToList();

    public DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash) => null;
}
