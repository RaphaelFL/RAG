namespace Chatbot.Application.Abstractions;

public interface IIngestionFailureRecorder
{
    void MarkIngestionFailure(Guid documentId, Guid jobId);
    void MarkReindexFailure(Guid documentId);
}