using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

public sealed class IngestionJobProcessor : IIngestionJobProcessor
{
    private readonly IIngestionBackgroundJobHandler _ingestionBackgroundJobHandler;
    private readonly IReindexBackgroundJobHandler _reindexBackgroundJobHandler;

    public IngestionJobProcessor(
        IIngestionBackgroundJobHandler ingestionBackgroundJobHandler,
        IReindexBackgroundJobHandler reindexBackgroundJobHandler)
    {
        _ingestionBackgroundJobHandler = ingestionBackgroundJobHandler;
        _reindexBackgroundJobHandler = reindexBackgroundJobHandler;
    }

    public Task ProcessIngestionAsync(IngestionBackgroundJob job, CancellationToken ct)
    {
        return _ingestionBackgroundJobHandler.ProcessAsync(job, ct);
    }

    public Task ProcessReindexAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        return _reindexBackgroundJobHandler.ProcessAsync(job, ct);
    }
}
