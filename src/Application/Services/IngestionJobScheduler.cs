using Chatbot.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Application.Services;

public sealed class IngestionJobScheduler : IIngestionJobScheduler
{
    private readonly IBackgroundJobQueue _backgroundJobQueue;

    public IngestionJobScheduler(IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue = backgroundJobQueue;
    }

    public Task ScheduleAsync(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, CancellationToken ct)
    {
        return _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
        {
            var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
            await processor.ProcessIngestionAsync(new IngestionBackgroundJob
            {
                JobId = jobId,
                DocumentId = context.DocumentId,
                TenantId = command.TenantId,
                FileName = command.FileName,
                ContentType = command.ContentType,
                ContentLength = command.ContentLength,
                DocumentTitle = command.DocumentTitle,
                Category = command.Category,
                Tags = new List<string>(command.Tags),
                Categories = new List<string>(command.Categories),
                Source = command.Source,
                ExternalId = command.ExternalId,
                AccessPolicy = command.AccessPolicy,
                Payload = context.Payload,
                RawHash = context.RawHash,
                StoragePath = context.StoragePath
            }, jobCt);
        }, ct).AsTask();
    }
}