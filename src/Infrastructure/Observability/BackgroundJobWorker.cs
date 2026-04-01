using Chatbot.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Observability;

public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(
        IBackgroundJobQueue backgroundJobQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobWorker> logger)
    {
        _backgroundJobQueue = backgroundJobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _backgroundJobQueue.DequeueAsync(stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled background job failure.");
            }
        }
    }
}