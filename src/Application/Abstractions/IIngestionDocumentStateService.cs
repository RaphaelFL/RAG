namespace Chatbot.Application.Abstractions;

public interface IIngestionDocumentStateService
{
    void UpdateStatus(Guid documentId, string status, Guid jobId);
    void CompleteIngestion(IngestionBackgroundJob job, int chunkCount);
    void CompleteReindex(DocumentCatalogEntry document, Guid jobId, int chunkCount);
    void CompleteFullReindex(DocumentCatalogEntry document, Guid jobId, int chunkCount, string contentHash, string contentType, string originalFileName);
    void MarkIngestionFailure(Guid documentId, Guid jobId);
    void MarkReindexFailure(Guid documentId);
}