using Microsoft.Extensions.DependencyInjection;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Authentication;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Providers;
using Chatbot.Infrastructure.Refit;
using Microsoft.Extensions.Options;
using Refit;

namespace Chatbot.Infrastructure;

/// <summary>
/// Registro de serviços de infraestrutura.
/// Providers mock e infraestrutura em memoria so podem ser usados com opt-in explicito de configuracao.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAzureAccessTokenProvider, AzureAccessTokenProvider>();
        services.AddSingleton<IApplicationCache, ApplicationCache>();
        services.AddSingleton<IDocumentCatalog>(serviceProvider =>
        {
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<FileSystemDocumentCatalog>(serviceProvider)
                : ActivatorUtilities.CreateInstance<InMemoryDocumentCatalog>(serviceProvider);
        });
        services.AddSingleton<IBackgroundJobQueue, InMemoryBackgroundJobQueue>();
        services.AddSingleton<IChatSessionStore, InMemoryChatSessionStore>();
        services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();
        services.AddSingleton<IDocumentAuthorizationService, DocumentAuthorizationService>();
        services.AddSingleton<IFeatureFlagService, RuntimeFeatureFlagService>();
        services.AddSingleton<RuntimeRagRuntimeSettings>();
        services.AddSingleton<IRagRuntimeSettings>(serviceProvider => serviceProvider.GetRequiredService<RuntimeRagRuntimeSettings>());
        services.AddSingleton<IRagRuntimeAdministrationService>(serviceProvider => serviceProvider.GetRequiredService<RuntimeRagRuntimeSettings>());
        services.AddSingleton<IPromptTemplateRegistry, PromptTemplateRegistry>();
        services.AddSingleton<IPromptInjectionDetector, PromptInjectionDetector>();
        services.AddSingleton<IMalwareScanner, SignatureMalwareScanner>();
        services.AddHostedService<ProviderConfigurationValidationHostedService>();
        services.AddHttpClient("OpenAICompatible")
            .ConfigureHttpClient(ConfigureOpenAiCompatibleClient);
        services.AddHttpClient("AzureOpenAI")
            .ConfigureHttpClient(ConfigureAzureOpenAiClient);
        services.AddHttpClient("AzureSearch")
            .ConfigureHttpClient(ConfigureAzureSearchClient);
        services.AddHttpClient("AzureDocumentIntelligence")
            .ConfigureHttpClient(ConfigureAzureDocumentIntelligenceClient);
        services.AddHttpClient("GoogleVision")
            .ConfigureHttpClient(ConfigureGoogleVisionClient);
        services.AddScoped<IChatCompletionProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var chatOptions = serviceProvider.GetRequiredService<IOptions<ChatModelOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return options.HasOpenAiCompatibleChatConfiguration(chatOptions.Model)
                ? ActivatorUtilities.CreateInstance<OpenAiCompatibleChatCompletionProvider>(serviceProvider)
                : options.HasAzureOpenAiChatConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureOpenAiChatCompletionProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockChatCompletionProvider>(serviceProvider)
                    : throw new InvalidOperationException("Nenhum provider de chat real esta configurado e o uso de mock foi desabilitado.");
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
                : options.HasAzureDocumentIntelligenceConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureDocumentIntelligenceOcrProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockOcrProvider>(serviceProvider)
                    : throw new InvalidOperationException("Azure Document Intelligence nao esta configurado e o uso de mock foi desabilitado.");
        });
        services.AddScoped<IEmbeddingProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var embeddingOptions = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            IEmbeddingProvider innerProvider = options.HasOpenAiCompatibleEmbeddingConfiguration(embeddingOptions.Model)
                ? ActivatorUtilities.CreateInstance<OpenAiCompatibleEmbeddingProvider>(serviceProvider)
                : options.HasAzureOpenAiEmbeddingConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureOpenAiEmbeddingProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockEmbeddingProvider>(serviceProvider)
                    : throw new InvalidOperationException("Nenhum provider de embeddings real esta configurado e o uso de mock foi desabilitado.");

            return ActivatorUtilities.CreateInstance<CachedEmbeddingProvider>(serviceProvider, innerProvider);
        });
        services.AddSingleton<IBlobStorageGateway>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<FileSystemBlobStorageGateway>(serviceProvider)
                : executionMode.PreferInMemoryInfrastructure
                ? executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemoryBlobStorageGateway>(serviceProvider)
                    : throw new InvalidOperationException("Modo local para Blob Storage foi solicitado, mas infraestrutura em memoria esta desabilitada.")
                : HasConfiguredValue(options.ConnectionString)
                ? ActivatorUtilities.CreateInstance<AzureBlobStorageGateway>(serviceProvider)
                : executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemoryBlobStorageGateway>(serviceProvider)
                    : throw new InvalidOperationException("Azure Blob Storage nao esta configurado e o uso de infraestrutura em memoria foi desabilitado.");
        });
        services.AddSingleton<ISearchIndexGateway>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<LocalPersistentSearchIndexGateway>(serviceProvider)
                : executionMode.PreferInMemoryInfrastructure
                ? executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider)
                    : throw new InvalidOperationException("Modo local para Search foi solicitado, mas infraestrutura em memoria esta desabilitada.")
                : options.HasAzureSearchConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureSearchIndexGateway>(serviceProvider)
                : executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider)
                    : throw new InvalidOperationException("Azure AI Search nao esta configurado e o uso de infraestrutura em memoria foi desabilitado.");
        });
        services.AddRefitClient<IAzureOpenAiClient>()
            .ConfigureHttpClient(ConfigureAzureOpenAiClient);
        services.AddRefitClient<IAzureSearchClient>()
            .ConfigureHttpClient(ConfigureAzureSearchClient);
        services.AddRefitClient<IBlobStorageClient>()
            .ConfigureHttpClient(ConfigureBlobStorageClient);
        services.AddRefitClient<IGoogleVisionClient>()
            .ConfigureHttpClient(ConfigureGoogleVisionClient);
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }

    private static void ConfigureAzureOpenAiClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(options.AzureOpenAiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureOpenAiCompatibleClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(NormalizeBaseUrl(options.OpenAiCompatibleBaseUrl));
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureAzureSearchClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(options.AzureSearchBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureBlobStorageClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(options.BlobStorageBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureAzureDocumentIntelligenceClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(options.AzureDocumentIntelligenceBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureGoogleVisionClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(options.GoogleVisionBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static bool HasConfiguredValue(string? value)
    {
        return ExternalProviderClientOptions.HasConfiguredValue(value);
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
