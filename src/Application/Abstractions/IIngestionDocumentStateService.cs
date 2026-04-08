namespace Chatbot.Application.Abstractions;

public interface IIngestionDocumentStateService : IIngestionStatusUpdater, IIngestionCompletionRecorder, IIngestionFailureRecorder
{
}