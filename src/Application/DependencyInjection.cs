using Chatbot.Application.Abstractions;
using Chatbot.Application.Agentic;
using Chatbot.Application.Configuration;
using Polly;
using Polly.Timeout;
using Chatbot.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Chatbot.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetService<IOptions<OperationalResilienceOptions>>()?.Value ?? new OperationalResilienceOptions();
            return CreateOperationalResiliencePipeline(options);
        });
        services.AddSingleton<IAgenticChatPlanner, LocalAgenticChatPlanner>();
        services.AddScoped<IChatOrchestrator, ChatOrchestratorService>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<IIngestionPipeline, IngestionService>();
        services.AddScoped<IDocumentMetadataSuggestionService, DocumentMetadataSuggestionService>();
        services.AddScoped<IIngestionJobProcessor, IngestionJobProcessor>();

        return services;
    }

    private static ResiliencePipeline CreateOperationalResiliencePipeline(OperationalResilienceOptions options)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds))
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