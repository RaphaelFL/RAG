using Microsoft.Extensions.DependencyInjection;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Agentic;
using Chatbot.Infrastructure.Embeddings;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Providers;
using Chatbot.Infrastructure.Tools;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure;

/// <summary>
/// Registro de serviços de infraestrutura.
/// Providers mock e infraestrutura em memoria so podem ser usados com opt-in explicito de configuracao.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationCache, ApplicationCache>();
        services.AddSingleton<IDocumentCatalog>(serviceProvider =>
        {
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<FileSystemDocumentCatalog>(serviceProvider)
                : ActivatorUtilities.CreateInstance<InMemoryDocumentCatalog>(serviceProvider);
        });
        services.AddSingleton<IBackgroundJobQueue, InMemoryBackgroundJobQueue>();
        services.AddSingleton<InMemoryChatSessionStore>();
        services.AddSingleton<IChatSessionStore>(serviceProvider =>
        {
            var redisCoordination = serviceProvider.GetRequiredService<IOptions<AppCfg.RedisCoordinationOptions>>().Value;
            var redisSettings = serviceProvider.GetRequiredService<IOptions<RedisSettings>>().Value;
            var hasRedisConfiguration = redisCoordination.Enabled
                && (!string.IsNullOrWhiteSpace(redisCoordination.Configuration)
                    || (!string.IsNullOrWhiteSpace(redisSettings.Server) && redisSettings.Port > 0));

            return hasRedisConfiguration
                ? ActivatorUtilities.CreateInstance<RedisChatSessionStore>(serviceProvider, serviceProvider.GetRequiredService<InMemoryChatSessionStore>())
                : serviceProvider.GetRequiredService<InMemoryChatSessionStore>();
        });
        services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();
        services.AddSingleton<IDocumentAuthorizationService, DocumentAuthorizationService>();
        services.AddSingleton<IFeatureFlagService, RuntimeFeatureFlagService>();
        services.AddSingleton<FileSystemOperationalAuditStore>();
        services.AddSingleton<IOperationalAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<FileSystemOperationalAuditStore>());
        services.AddSingleton<IOperationalAuditReader>(serviceProvider => serviceProvider.GetRequiredService<FileSystemOperationalAuditStore>());
        services.AddSingleton<RuntimeRagRuntimeSettings>();
        services.AddSingleton<IRagRuntimeSettings>(serviceProvider => serviceProvider.GetRequiredService<RuntimeRagRuntimeSettings>());
        services.AddSingleton<IRagRuntimeAdministrationService>(serviceProvider => serviceProvider.GetRequiredService<RuntimeRagRuntimeSettings>());
        services.AddSingleton<IPromptTemplateRegistry, PromptTemplateRegistry>();
        services.AddSingleton<IPromptInjectionDetector, PromptInjectionDetector>();
        services.AddSingleton<IMalwareScanner, SignatureMalwareScanner>();
        services.AddSingleton<IVectorStore, SearchIndexBackedVectorStore>();
        services.AddHostedService<ProviderConfigurationValidationHostedService>();
        services.AddHttpClient("OpenAICompatible")
            .ConfigureHttpClient(ConfigureOpenAiCompatibleClient);
        services.AddHttpClient("WebSearch")
            .ConfigureHttpClient(ConfigureWebSearchClient);
        services.AddScoped<IChatCompletionProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var chatOptions = serviceProvider.GetRequiredService<IOptions<ChatModelOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return options.HasOpenAiCompatibleChatConfiguration(chatOptions.Model)
                ? ActivatorUtilities.CreateInstance<OpenAiCompatibleChatCompletionProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockChatCompletionProvider>(serviceProvider)
                    : throw new InvalidOperationException("Nenhum provider de chat local esta configurado e o uso de mock foi desabilitado.");
        });
        services.AddScoped<IOcrProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalOcr && options.HasOpenAiCompatibleVisionConfiguration()
                ? ActivatorUtilities.CreateInstance<LocalVisionOcrProvider>(serviceProvider)
                : executionMode.PreferMockProviders
                ? executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockOcrProvider>(serviceProvider)
                    : throw new InvalidOperationException("Modo local para OCR foi solicitado, mas mocks estao desabilitados.")
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockOcrProvider>(serviceProvider)
                    : throw new InvalidOperationException("OCR local nao esta configurado e o uso de mock foi desabilitado.");
        });
        services.AddScoped<IEmbeddingProvider>(serviceProvider =>
        {
            var embeddingRuntimeOptions = serviceProvider.GetRequiredService<IOptions<AppCfg.EmbeddingGenerationOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;

            IEmbeddingProvider innerProvider = string.Equals(embeddingRuntimeOptions.PrimaryRuntime, "python-local", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<InternalProcessEmbeddingProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockEmbeddingProvider>(serviceProvider)
                    : throw new InvalidOperationException("Somente o runtime interno de embeddings esta habilitado nesta configuracao.");

            return ActivatorUtilities.CreateInstance<CachedEmbeddingProvider>(serviceProvider, innerProvider);
        });
        services.AddSingleton<IBlobStorageGateway>(serviceProvider =>
        {
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<FileSystemBlobStorageGateway>(serviceProvider)
                : executionMode.PreferInMemoryInfrastructure
                ? executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemoryBlobStorageGateway>(serviceProvider)
                    : throw new InvalidOperationException("Modo local para Blob Storage foi solicitado, mas infraestrutura em memoria esta desabilitada.")
                : executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemoryBlobStorageGateway>(serviceProvider)
                    : throw new InvalidOperationException("Armazenamento local de documentos nao esta configurado e o uso de infraestrutura em memoria foi desabilitado.");
        });
        services.AddSingleton<ISearchIndexGateway>(serviceProvider =>
        {
            var vectorStoreOptions = serviceProvider.GetRequiredService<IOptions<AppCfg.VectorStoreOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            ISearchIndexGateway localFallback;
            if (executionMode.PreferInMemoryInfrastructure)
            {
                localFallback = executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider)
                    : throw new InvalidOperationException("Modo local para Search foi solicitado, mas infraestrutura em memoria esta desabilitada.");
            }
            else
            {
                localFallback = ActivatorUtilities.CreateInstance<LocalPersistentSearchIndexGateway>(serviceProvider);
            }

            if (string.Equals(vectorStoreOptions.Provider, "pgvector", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<PgVectorSearchIndexGateway>(serviceProvider, localFallback);
            }

            if (string.Equals(vectorStoreOptions.Provider, "redisstack", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<RedisStackSearchIndexGateway>(serviceProvider, localFallback);
            }

            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<LocalPersistentSearchIndexGateway>(serviceProvider)
                : executionMode.PreferInMemoryInfrastructure
                ? executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider)
                    : throw new InvalidOperationException("Modo local para Search foi solicitado, mas infraestrutura em memoria esta desabilitada.")
                : executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider)
                    : throw new InvalidOperationException("Nenhum backend local de busca foi configurado e o uso de infraestrutura em memoria foi desabilitado.");
        });
        services.AddScoped<IAgentRuntime, SemanticKernelAgentRuntime>();
        services.AddScoped<IWebSearchTool, GuardedWebSearchTool>();
        services.AddScoped<ICodeInterpreter, GuardedPythonCodeInterpreter>();
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }

    private static void ConfigureOpenAiCompatibleClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(NormalizeBaseUrl(options.OpenAiCompatibleBaseUrl));
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureWebSearchClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AppCfg.WebSearchOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(2, options.TimeoutSeconds));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ChatbotRagPlatform/1.0 (+https://localhost)");
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : baseUrl + "/";
    }
}
