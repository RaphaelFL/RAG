using Microsoft.Extensions.DependencyInjection;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Providers;
using Chatbot.Infrastructure.Refit;
using Microsoft.Extensions.Options;
using Refit;

namespace Chatbot.Infrastructure;

/// <summary>
/// Registro de serviços de infraestrutura.
/// Quando os providers reais (Azure) forem implementados, registrá-los aqui.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentCatalog, InMemoryDocumentCatalog>();
        services.AddSingleton<IBackgroundJobQueue, InMemoryBackgroundJobQueue>();
        services.AddSingleton<IChatSessionStore, InMemoryChatSessionStore>();
        services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();
        services.AddSingleton<IDocumentAuthorizationService, DocumentAuthorizationService>();
        services.AddSingleton<IFeatureFlagService, RuntimeFeatureFlagService>();
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
            return options.HasAzureOpenAiChatConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureOpenAiChatCompletionProvider>(serviceProvider)
                : ActivatorUtilities.CreateInstance<MockChatCompletionProvider>(serviceProvider);
        });
        services.AddScoped<IOcrProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            return options.HasAzureDocumentIntelligenceConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureDocumentIntelligenceOcrProvider>(serviceProvider)
                : ActivatorUtilities.CreateInstance<MockOcrProvider>(serviceProvider);
        });
        services.AddScoped<IEmbeddingProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            return options.HasAzureOpenAiEmbeddingConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureOpenAiEmbeddingProvider>(serviceProvider)
                : ActivatorUtilities.CreateInstance<MockEmbeddingProvider>(serviceProvider);
        });
        services.AddSingleton<IBlobStorageGateway>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
            return HasConfiguredValue(options.ConnectionString)
                ? ActivatorUtilities.CreateInstance<AzureBlobStorageGateway>(serviceProvider)
                : ActivatorUtilities.CreateInstance<InMemoryBlobStorageGateway>(serviceProvider);
        });
        services.AddSingleton<ISearchIndexGateway>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
            return options.HasAzureSearchConfiguration()
                ? ActivatorUtilities.CreateInstance<AzureSearchIndexGateway>(serviceProvider)
                : ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider);
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
