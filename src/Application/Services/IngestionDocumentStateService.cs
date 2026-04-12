namespace Chatbot.Application.Services;

public sealed class IngestionDocumentStateService : IIngestionDocumentStateService
{
    private readonly IDocumentCatalog _documentCatalog;

    public IngestionDocumentStateService(IDocumentCatalog documentCatalog)
    {
        _documentCatalog = documentCatalog;
    }

    public void UpdateStatus(Guid documentId, string status, Guid jobId)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return;
        }

        document.Status = status;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);
    }

    public void CompleteIngestion(IngestionBackgroundJob job, int chunkCount)
    {
        var document = _documentCatalog.Get(job.DocumentId);
        if (document is null)
        {
            return;
        }

        document.Status = DocumentStatuses.Indexed;
        document.StoragePath = job.StoragePath;
        document.QuarantinePath = null;
        document.ContentHash = job.RawHash;
        document.OriginalFileName = job.FileName;
        document.IndexedChunkCount = chunkCount;
        document.Chunks = new List<DocumentChunkIndexDto>();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = job.JobId;
        _documentCatalog.Upsert(document);
    }

    public void CompleteReindex(DocumentCatalogEntry document, Guid jobId, int chunkCount)
    {
        document.Version += 1;
        document.Status = DocumentStatuses.Indexed;
        document.IndexedChunkCount = chunkCount;
        document.Chunks = new List<DocumentChunkIndexDto>();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);
    }

    public void CompleteFullReindex(DocumentCatalogEntry document, Guid jobId, int chunkCount, string contentHash, string contentType, string originalFileName)
    {
        document.Version += 1;
        document.Status = DocumentStatuses.Indexed;
        document.ContentHash = contentHash;
        document.ContentType = contentType;
        document.OriginalFileName = originalFileName;
        document.IndexedChunkCount = chunkCount;
        document.Chunks = new List<DocumentChunkIndexDto>();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);
    }

    public void MarkIngestionFailure(Guid documentId, Guid jobId)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return;
        }

        document.Status = DocumentStatuses.Failed;
        document.IndexedChunkCount = 0;
        document.Chunks = new List<DocumentChunkIndexDto>();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);
    }

    public void MarkReindexFailure(Guid documentId)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return;
        }

        document.Status = DocumentStatuses.Failed;
        document.UpdatedAtUtc = DateTime.UtcNow;
        _documentCatalog.Upsert(document);
    }
}