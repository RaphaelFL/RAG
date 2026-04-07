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
        services.AddSingleton<NoOpOperationalAuditStore>();
        services.AddScoped<IChatRequestTemplateResolver, ChatRequestTemplateResolver>();
        services.AddSingleton<IChatEvidenceSelector, ChatEvidenceSelector>();
        services.AddSingleton<IChatStreamingSegmenter, ChatStreamingSegmenter>();
        services.AddSingleton<IChatCompletionCacheKeyFactory, ChatCompletionCacheKeyFactory>();
        services.AddScoped<IGovernedAgentHandler, FileSearchAgentHandler>();
        services.AddScoped<IGovernedAgentHandler, WebSearchAgentHandler>();
        services.AddScoped<IGovernedAgentHandler, CodeInterpreterAgentHandler>();
        services.AddScoped<IGovernedAgentHandler, PromptAssemblyAgentHandler>();
        services.AddSingleton<IAgenticChatPlanner, LocalAgenticChatPlanner>();
        services.AddScoped<IChatOrchestrator, ChatOrchestratorService>();
        services.AddSingleton<IRetrievalCacheKeyFactory, RetrievalCacheKeyFactory>();
        services.AddScoped<IRetrievalChunkSelector, RetrievalChunkSelector>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<ISearchQueryService, SearchQueryService>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<IDocumentReindexService, DocumentReindexService>();
        services.AddScoped<IDocumentQueryService, DocumentQueryService>();
        services.AddScoped<IDocumentMetadataExtractionService, DocumentMetadataExtractionService>();
        services.AddSingleton<IDocumentMetadataTitleSuggester, DocumentMetadataTitleSuggester>();
        services.AddSingleton<IDocumentMetadataCategorySuggester, DocumentMetadataCategorySuggester>();
        services.AddSingleton<IDocumentMetadataTagSuggester, DocumentMetadataTagSuggester>();
        services.AddSingleton<IDocumentMetadataPreviewBuilder, DocumentMetadataPreviewBuilder>();
        services.AddScoped<IDocumentMetadataSuggestionService, DocumentMetadataSuggestionService>();
        services.AddScoped<IIngestionCommandFactory, IngestionCommandFactory>();
        services.AddScoped<IIngestionExtractionService, IngestionExtractionService>();
        services.AddScoped<IIngestionChunkEnricher, IngestionChunkEnricher>();
        services.AddScoped<IIngestionDocumentStateService, IngestionDocumentStateService>();
        services.AddScoped<IIngestionJobProcessor, IngestionJobProcessor>();
        services.AddScoped<IEmbeddingGenerationService, CurrentStateEmbeddingGenerationService>();
        services.AddScoped<IRetriever, CurrentStateRetrieverAdapter>();
        services.AddScoped<IReranker, CurrentStateRerankerAdapter>();
        services.AddSingleton<IOperationalAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<NoOpOperationalAuditStore>());
        services.AddSingleton<IOperationalAuditReader>(serviceProvider => serviceProvider.GetRequiredService<NoOpOperationalAuditStore>());
        services.AddScoped<IPromptAssembler, CurrentStatePromptAssembler>();
        services.AddScoped<IFileSearchTool, CurrentStateFileSearchTool>();
        services.AddScoped<IWebSearchTool, DisabledWebSearchTool>();
        services.AddScoped<ICodeInterpreter, DisabledCodeInterpreter>();
        services.AddScoped<IAgentRuntime, GovernedAgentRuntime>();

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