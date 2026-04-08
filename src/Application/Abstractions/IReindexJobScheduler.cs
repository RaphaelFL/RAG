namespace Chatbot.Application.Abstractions;

public interface IReindexJobScheduler
{
    Task ScheduleAsync(Guid jobId, Guid documentId, bool fullReindex, string? forceEmbeddingModel, CancellationToken ct);
}