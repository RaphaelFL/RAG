using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure;

internal static class InfrastructurePersistenceRegistration
{
    public static IServiceCollection AddPersistenceInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentCatalog>(serviceProvider =>
        {
            var executionMode = serviceProvider.GetRequiredService<IOptions<ProviderExecutionModeOptions>>().Value;
            return executionMode.PreferLocalPersistentInfrastructure
                ? ActivatorUtilities.CreateInstance<FileSystemDocumentCatalog>(serviceProvider)
                : ActivatorUtilities.CreateInstance<InMemoryDocumentCatalog>(serviceProvider);
        });

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
            var localFallback = CreateLocalSearchFallback(serviceProvider, executionMode);

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

        return services;
    }

    private static ISearchIndexGateway CreateLocalSearchFallback(IServiceProvider serviceProvider, ProviderExecutionModeOptions executionMode)
    {
        if (executionMode.PreferInMemoryInfrastructure)
        {
            return executionMode.AllowInMemoryInfrastructure
                ? ActivatorUtilities.CreateInstance<InMemorySearchIndexGateway>(serviceProvider)
                : throw new InvalidOperationException("Modo local para Search foi solicitado, mas infraestrutura em memoria esta desabilitada.");
        }

        return ActivatorUtilities.CreateInstance<LocalPersistentSearchIndexGateway>(serviceProvider);
    }
}