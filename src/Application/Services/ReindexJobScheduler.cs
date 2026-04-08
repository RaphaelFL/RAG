using Chatbot.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Application.Services;

internal sealed class ReindexJobScheduler : IReindexJobScheduler
{
    private readonly IBackgroundJobQueue _backgroundJobQueue;

    public ReindexJobScheduler(IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue = backgroundJobQueue;
    }

    public Task ScheduleAsync(Guid jobId, Guid documentId, bool fullReindex, string? forceEmbeddingModel, CancellationToken ct)
    {
        return _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
        {
            var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
            await processor.ProcessReindexAsync(new ReindexBackgroundJob
            {
                JobId = jobId,
                DocumentId = documentId,
                FullReindex = fullReindex,
                ForceEmbeddingModel = forceEmbeddingModel
            }, jobCt);
        }, ct).AsTask();
    }
}