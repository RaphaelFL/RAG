namespace Chatbot.Application.Abstractions;

public interface IIngestionStatusUpdater
{
    void UpdateStatus(Guid documentId, string status, Guid jobId);
}