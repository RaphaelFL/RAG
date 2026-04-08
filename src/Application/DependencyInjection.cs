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
        services.AddSingleton<NoOpOperationalAuditWriter>();
        services.AddSingleton<NoOpOperationalAuditReader>();
        services.AddScoped<IChatRequestTemplateResolver, ChatRequestTemplateResolver>();
        services.AddScoped<ChatRetrievalContextBuilder>();
        services.AddScoped<ChatCompletionResponseBuilder>();
        services.AddScoped<PreparedChatTurnFactory>();
        services.AddScoped<IChatTurnPreparer, ChatTurnPreparer>();
        services.AddScoped<IChatTurnPersister, ChatTurnPersister>();
        services.AddSingleton<IChatEvidenceSelector, ChatEvidenceSelector>();
        services.AddSingleton<IChatStreamingSegmenter, ChatStreamingSegmenter>();
        services.AddSingleton<IChatCompletionCacheKeyFactory, ChatCompletionCacheKeyFactory>();
        services.AddScoped<IGovernedAgentHandler, FileSearchAgentHandler>();
        services.AddScoped<IGovernedAgentHandler, WebSearchAgentHandler>();
        services.AddScoped<IGovernedAgentHandler, CodeInterpreterAgentHandler>();
        services.AddScoped<IGovernedAgentHandler, PromptAssemblyAgentHandler>();
        services.AddSingleton<IAgenticChatPlanner, LocalAgenticChatPlanner>();
        services.AddScoped<IChatOrchestrator>(serviceProvider => new ChatOrchestratorService(
            serviceProvider.GetRequiredService<IAgenticChatPlanner>(),
            serviceProvider.GetRequiredService<IChatRequestTemplateResolver>(),
            serviceProvider.GetRequiredService<IChatStreamingSegmenter>(),
            serviceProvider.GetRequiredService<IChatTurnPreparer>(),
            serviceProvider.GetRequiredService<IChatTurnPersister>(),
            serviceProvider.GetRequiredService<ILogger<ChatOrchestratorService>>()));
        services.AddSingleton<IRetrievalCacheKeyFactory, RetrievalCacheKeyFactory>();
        services.AddScoped<IRetrievalQueryPlanner, RetrievalQueryPlanner>();
        services.AddScoped<IRetrievalResultAuthorizer, RetrievalResultAuthorizer>();
        services.AddScoped<IRetrievalAuditLogger, RetrievalAuditLogger>();
        services.AddScoped<IRetrievalChunkSelector, RetrievalChunkSelector>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<ISearchQueryService, SearchQueryService>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<IDocumentReindexService>(serviceProvider => new DocumentReindexService(
            serviceProvider.GetRequiredService<IDocumentCatalog>(),
            serviceProvider.GetRequiredService<IDocumentReindexDocumentResolver>(),
            serviceProvider.GetRequiredService<IDocumentReindexAccessGuard>(),
            serviceProvider.GetRequiredService<IReindexJobScheduler>(),
            serviceProvider.GetRequiredService<ILogger<DocumentReindexService>>()));
        services.AddScoped<IDocumentReindexDocumentResolver, DocumentReindexDocumentResolver>();
        services.AddScoped<IDocumentReindexAccessGuard, DocumentReindexAccessGuard>();
        services.AddScoped<IReindexJobScheduler, ReindexJobScheduler>();
        services.AddScoped<IDocumentQueryAccessGuard, DocumentQueryAccessGuard>();
        services.AddScoped<IDocumentInspectionBuilder, DocumentInspectionBuilder>();
        services.AddScoped<IDocumentQueryService>(serviceProvider => new DocumentQueryService(
            serviceProvider.GetRequiredService<IDocumentCatalog>(),
            serviceProvider.GetRequiredService<ISearchIndexGateway>(),
            serviceProvider.GetRequiredService<IDocumentQueryAccessGuard>(),
            serviceProvider.GetRequiredService<IDocumentInspectionBuilder>()));
        services.AddScoped<IDocumentMetadataExtractionService, DocumentMetadataExtractionService>();
        services.AddSingleton<IDocumentMetadataTitleSuggester, DocumentMetadataTitleSuggester>();
        services.AddSingleton<IDocumentMetadataCategorySuggester, DocumentMetadataCategorySuggester>();
        services.AddSingleton<IDocumentMetadataTagSuggester, DocumentMetadataTagSuggester>();
        services.AddSingleton<IDocumentMetadataPreviewBuilder, DocumentMetadataPreviewBuilder>();
        services.AddScoped<IDocumentMetadataSuggestionService, DocumentMetadataSuggestionService>();
        services.AddScoped<IIngestionContentStorage, IngestionContentStorage>();
        services.AddScoped<IIngestionCatalogEntryFactory, IngestionCatalogEntryFactory>();
        services.AddScoped<IIngestionJobScheduler, IngestionJobScheduler>();
        services.AddScoped<IIngestionCommandFactory, IngestionCommandFactory>();
        services.AddScoped<IIngestionExtractionService, IngestionExtractionService>();
        services.AddScoped<IIngestionChunkEnricher, IngestionChunkEnricher>();
        services.AddScoped<IIngestionDocumentStateService, IngestionDocumentStateService>();
        services.AddScoped<IIngestionBackgroundJobHandler, IngestionBackgroundJobHandler>();
        services.AddScoped<IFullReindexProcessor, FullReindexProcessor>();
        services.AddScoped<IIncrementalReindexProcessor, IncrementalReindexProcessor>();
        services.AddScoped<IReindexBackgroundJobHandler>(serviceProvider => new ReindexBackgroundJobHandler(
            serviceProvider.GetRequiredService<IFullReindexProcessor>(),
            serviceProvider.GetRequiredService<IIncrementalReindexProcessor>(),
            serviceProvider.GetRequiredService<IIngestionDocumentStateService>(),
            serviceProvider.GetRequiredService<ILogger<ReindexBackgroundJobHandler>>()));
        services.AddScoped<IIngestionJobProcessor, IngestionJobProcessor>();
        services.AddScoped<IEmbeddingGenerationService, CurrentStateEmbeddingGenerationService>();
        services.AddScoped<IRetriever, CurrentStateRetrieverAdapter>();
        services.AddScoped<IReranker, CurrentStateRerankerAdapter>();
        services.AddSingleton<IOperationalAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<NoOpOperationalAuditWriter>());
        services.AddSingleton<IOperationalAuditReader>(serviceProvider => serviceProvider.GetRequiredService<NoOpOperationalAuditReader>());
        services.AddScoped<IPromptChunkSelector, PromptChunkSelector>();
        services.AddScoped<IPromptContentBuilder, PromptContentBuilder>();
        services.AddScoped<IPromptAssemblyAuditLogger, PromptAssemblyAuditLogger>();
        services.AddScoped<IPromptAssembler>(serviceProvider => new CurrentStatePromptAssembler(
            serviceProvider.GetRequiredService<IPromptChunkSelector>(),
            serviceProvider.GetRequiredService<IPromptContentBuilder>(),
            serviceProvider.GetRequiredService<IPromptAssemblyAuditLogger>()));
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