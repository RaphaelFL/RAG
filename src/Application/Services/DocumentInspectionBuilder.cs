using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

internal sealed class DocumentInspectionBuilder : IDocumentInspectionBuilder
{
    public DocumentInspectionDto Build(
        DocumentCatalogEntry document,
        IReadOnlyCollection<DocumentChunkIndexDto> chunks,
        string? search,
        int pageNumber,
        int pageSize)
    {
        var sanitizedPageNumber = Math.Max(1, pageNumber);
        var sanitizedPageSize = Math.Clamp(pageSize, 1, 100);
        var orderedChunks = chunks
            .OrderBy(DocumentQueryMapper.ResolveChunkIndex)
            .ThenBy(chunk => chunk.PageNumber)
            .ThenBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filteredChunks = string.IsNullOrWhiteSpace(search)
            ? orderedChunks
            : orderedChunks.Where(chunk => ChunkMatchesSearch(chunk, search)).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredChunks.Count / (double)sanitizedPageSize));
        var resolvedPageNumber = Math.Min(sanitizedPageNumber, totalPages);
        var pagedChunks = filteredChunks
            .Skip((resolvedPageNumber - 1) * sanitizedPageSize)
            .Take(sanitizedPageSize)
            .Select(DocumentQueryMapper.MapChunkInspection)
            .ToList();

        return new DocumentInspectionDto
        {
            Document = DocumentQueryMapper.MapDocumentDetails(document),
            EmbeddedChunkCount = orderedChunks.Count(chunk => chunk.Embedding is { Length: > 0 }),
            TotalChunkCount = orderedChunks.Count,
            FilteredChunkCount = filteredChunks.Count,
            PageNumber = resolvedPageNumber,
            PageSize = sanitizedPageSize,
            TotalPages = totalPages,
            Chunks = pagedChunks
        };
    }

    private static bool ChunkMatchesSearch(DocumentChunkIndexDto chunk, string search)
    {
        var normalizedSearch = search.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return true;
        }

        if (chunk.ChunkId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || chunk.Content.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(chunk.Section) && chunk.Section.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return chunk.Metadata.Any(pair =>
            pair.Key.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || pair.Value.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
    }
}