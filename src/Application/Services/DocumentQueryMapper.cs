namespace Chatbot.Application.Services;

internal static class DocumentQueryMapper
{
    public static DocumentDetailsDto MapDocumentDetails(DocumentCatalogEntry document)
    {
        return new DocumentDetailsDto
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            Status = document.Status,
            Version = document.Version,
            IndexedChunkCount = ResolveIndexedChunkCount(document),
            ContentType = document.ContentType,
            Source = document.Source,
            LastJobId = document.LastJobId,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            Metadata = new DocumentMetadataDto
            {
                Category = document.Category,
                Tags = document.Tags,
                Categories = document.Categories,
                ExternalId = document.ExternalId,
                AccessPolicy = document.AccessPolicy
            }
        };
    }

    public static DocumentChunkInspectionDto MapChunkInspection(DocumentChunkIndexDto chunk)
    {
        var dimensions = chunk.Embedding?.Length ?? 0;
        return new DocumentChunkInspectionDto
        {
            ChunkId = chunk.ChunkId,
            ChunkIndex = ResolveChunkIndex(chunk),
            Content = chunk.Content,
            CharacterCount = chunk.Content.Length,
            PageNumber = chunk.PageNumber,
            EndPageNumber = ResolveMetadataInt(chunk.Metadata, "endPage"),
            Section = chunk.Section,
            Metadata = new Dictionary<string, string>(chunk.Metadata),
            Embedding = new DocumentEmbeddingInspectionDto
            {
                Exists = dimensions > 0,
                Dimensions = dimensions,
                Preview = chunk.Embedding?.Take(8).ToList() ?? new List<float>()
            }
        };
    }

    public static int ResolveChunkIndex(DocumentChunkIndexDto chunk)
    {
        return ResolveMetadataInt(chunk.Metadata, "chunkIndex") ?? int.MaxValue;
    }

    private static int? ResolveMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private static int ResolveIndexedChunkCount(DocumentCatalogEntry document)
    {
        return document.IndexedChunkCount > 0
            ? document.IndexedChunkCount
            : document.Chunks.Count;
    }
}