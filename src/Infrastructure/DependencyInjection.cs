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
        services.AddSingleton<IDocumentCatalog, InMemoryDocumentCatalog>();
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
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return options.HasAzureOpenAiChatConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureOpenAiChatCompletionProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockChatCompletionProvider>(serviceProvider)
                    : throw new InvalidOperationException("Azure OpenAI chat nao esta configurado e o uso de mock foi desabilitado.");
        });
        services.AddScoped<IOcrProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return options.HasAzureDocumentIntelligenceConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureDocumentIntelligenceOcrProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockOcrProvider>(serviceProvider)
                    : throw new InvalidOperationException("Azure Document Intelligence nao esta configurado e o uso de mock foi desabilitado.");
        });
        services.AddScoped<IEmbeddingProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            IEmbeddingProvider innerProvider = options.HasAzureOpenAiEmbeddingConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureOpenAiEmbeddingProvider>(serviceProvider)
                : executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockEmbeddingProvider>(serviceProvider)
                    : throw new InvalidOperationException("Azure OpenAI embeddings nao esta configurado e o uso de mock foi desabilitado.");

            return ActivatorUtilities.CreateInstance<CachedEmbeddingProvider>(serviceProvider, innerProvider);
        });
        services.AddSingleton<IBlobStorageGateway>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return HasConfiguredValue(options.ConnectionString)
                ? ActivatorUtilities.CreateInstance<AzureBlobStorageGateway>(serviceProvider)
                : executionMode.AllowInMemoryInfrastructure
                    ? ActivatorUtilities.CreateInstance<InMemoryBlobStorageGateway>(serviceProvider)
                    : throw new InvalidOperationException("Azure Blob Storage nao esta configurado e o uso de infraestrutura em memoria foi desabilitado.");
        });
        services.AddSingleton<ISearchIndexGateway>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return options.HasAzureSearchConfiguration()
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
}
