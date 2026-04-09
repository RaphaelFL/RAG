using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Agentic;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Embeddings;
using Chatbot.Infrastructure.Providers;
using Chatbot.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure;

internal static class InfrastructureProviderRegistration
{
    public static IServiceCollection AddProviderInfrastructure(this IServiceCollection services)
    {
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

            IEmbeddingProvider innerProvider;
            if (string.Equals(embeddingRuntimeOptions.PrimaryRuntime, "python-local", StringComparison.OrdinalIgnoreCase))
            {
                var primaryProvider = ActivatorUtilities.CreateInstance<InternalProcessEmbeddingProvider>(serviceProvider);

                innerProvider = executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<ResilientEmbeddingProvider>(
                        serviceProvider,
                        primaryProvider,
                        ActivatorUtilities.CreateInstance<MockEmbeddingProvider>(serviceProvider),
                        "python-local",
                        nameof(MockEmbeddingProvider))
                    : primaryProvider;
            }
            else
            {
                innerProvider = executionMode.AllowMockProviders
                    ? ActivatorUtilities.CreateInstance<MockEmbeddingProvider>(serviceProvider)
                    : throw new InvalidOperationException("Somente o runtime interno de embeddings esta habilitado nesta configuracao.");
            }

            return ActivatorUtilities.CreateInstance<CachedEmbeddingProvider>(serviceProvider, innerProvider);
        });

        services.AddScoped<IAgentRuntime, SemanticKernelAgentRuntime>();
        services.AddScoped<IWebSearchTool, GuardedWebSearchTool>();
        services.AddScoped<ICodeInterpreter, GuardedPythonCodeInterpreter>();

        return services;
    }
}