using Chatbot.Application.Abstractions;
using Chatbot.Application.Agentic;
using Polly;
using Polly.Timeout;
using Chatbot.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(CreateOperationalResiliencePipeline());
        services.AddSingleton<IAgenticChatPlanner, LocalAgenticChatPlanner>();
        services.AddScoped<IChatOrchestrator, ChatOrchestratorService>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<IIngestionPipeline, IngestionService>();
        services.AddScoped<IIngestionJobProcessor, IngestionJobProcessor>();

        return services;
    }

    private static ResiliencePipeline CreateOperationalResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(10))
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(150),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutRejectedException>()
                    .Handle<IOException>()
            })
            .Build();
    }
}