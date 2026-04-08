using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Infrastructure;

internal static class InfrastructureCoreRegistration
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationCache, ApplicationCache>();
        services.AddSingleton<IBackgroundJobQueue, InMemoryBackgroundJobQueue>();
        services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();
        services.AddSingleton<IDocumentAuthorizationService, DocumentAuthorizationService>();
        services.AddSingleton<IFeatureFlagService, RuntimeFeatureFlagService>();
        services.AddSingleton<FileSystemOperationalAuditStore>();
        services.AddSingleton<IOperationalAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<FileSystemOperationalAuditStore>());
        services.AddSingleton<IOperationalAuditReader>(serviceProvider => serviceProvider.GetRequiredService<FileSystemOperationalAuditStore>());
        services.AddSingleton<RagRuntimeSettingsStore>();
        services.AddSingleton<IRagRuntimeSettings, RuntimeRagRuntimeSettings>();
        services.AddSingleton<IRagRuntimeAdministrationService, RuntimeRagRuntimeAdministrationService>();
        services.AddSingleton<IPromptTemplateRegistry, PromptTemplateRegistry>();
        services.AddSingleton<IPromptInjectionDetector, PromptInjectionDetector>();
        services.AddSingleton<IMalwareScanner, SignatureMalwareScanner>();
        services.AddSingleton<IVectorStore, SearchIndexBackedVectorStore>();
        services.AddHostedService<ProviderConfigurationValidationHostedService>();
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }
}