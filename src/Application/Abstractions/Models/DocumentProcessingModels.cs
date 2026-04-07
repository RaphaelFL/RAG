namespace Chatbot.Application.Abstractions;

public class OcrResultDto
{
    public string ExtractedText { get; set; } = string.Empty;
    public List<PageExtractionDto> Pages { get; set; } = new();
    public string? Provider { get; set; }
}

public class DocumentTextExtractionResultDto
{
    public string Text { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? StructuredJson { get; set; }
    public List<PageExtractionDto> Pages { get; set; } = new();
}

public class DocumentParseResultDto
{
    public string Text { get; set; } = string.Empty;
    public string? StructuredJson { get; set; }
    public List<PageExtractionDto> Pages { get; set; } = new();
}

public class PageExtractionDto
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? WorksheetName { get; set; }
    public int? SlideNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string? TableId { get; set; }
    public string? FormId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<TableDto>? Tables { get; set; }
}

public class TableDto
{
    public List<List<string>> Rows { get; set; } = new();
}

public class DocumentChunkIndexDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public int PageNumber { get; set; }
    public string? Section { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SearchResultDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class FileSearchFilterDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
    public Guid? TenantId { get; set; }
}

public class IngestDocumentCommand
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string? DocumentTitle { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
    public Stream Content { get; set; } = Stream.Null;
}

public sealed class IngestionBackgroundJob
{
    public Guid JobId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string? DocumentTitle { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public string RawHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}

public sealed class ReindexBackgroundJob
{
    public Guid JobId { get; set; }
    public Guid DocumentId { get; set; }
    public bool FullReindex { get; set; }
    public string? ForceEmbeddingModel { get; set; }
}

public class DocumentCatalogEntry
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Source { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
    public string? StoragePath { get; set; }
    public string? QuarantinePath { get; set; }
    public Guid? LastJobId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int IndexedChunkCount { get; set; }
    public List<DocumentChunkIndexDto> Chunks { get; set; } = new();
}

public class MalwareScanResultDto
{
    public bool IsSafe { get; set; }
    public bool RequiresQuarantine { get; set; }
    public string? Reason { get; set; }
}