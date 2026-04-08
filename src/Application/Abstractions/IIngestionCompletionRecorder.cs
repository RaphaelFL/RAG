namespace Chatbot.Application.Abstractions;

public interface IIngestionCompletionRecorder
{
    void CompleteIngestion(IngestionBackgroundJob job, int chunkCount);
    void CompleteReindex(DocumentCatalogEntry document, Guid jobId, int chunkCount);
    void CompleteFullReindex(DocumentCatalogEntry document, Guid jobId, int chunkCount, string contentHash, string contentType, string originalFileName);
}