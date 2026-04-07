using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using AppCfg = Chatbot.Application.Configuration;

using Backend.Unit.InfrastructureRegistrationTestsSupport;

namespace Backend.Unit;

public class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_ShouldRegisterCoreLocalServicesAndProviderOptions()
    {
        var services = new ServiceCollection();
        AddRuntimePrerequisites(services);
        services.Configure<ExternalProviderClientOptions>(options =>
        {
            options.TimeoutSeconds = 12;
            options.OpenAiCompatibleBaseUrl = "http://localhost:11434/v1";
            options.OpenAiCompatibleChatModel = "qwen2.5-coder:7b";
            options.OpenAiCompatibleVisionModel = "llava";
        });
        services.Configure<ChatModelOptions>(options => options.Model = "qwen2.5-coder:7b");
        services.Configure<EmbeddingOptions>(options =>
        {
            options.Model = "nomic-embed-text";
            options.Dimensions = 768;
        });
        services.Configure<LocalPersistenceOptions>(options =>
        {
            options.BasePath = Path.Combine(Path.GetTempPath(), "rag-test-registration");
        });
        services.Configure<ProviderExecutionModeOptions>(options =>
        {
            options.AllowMockProviders = true;
            options.AllowInMemoryInfrastructure = true;
            options.PreferLocalPersistentInfrastructure = true;
            options.PreferLocalOcr = true;
        });

        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IChatCompletionProvider>().Should().NotBeNull();
        provider.GetService<IEmbeddingProvider>().Should().NotBeNull();
        provider.GetService<IOcrProvider>().Should().NotBeNull();
        provider.GetService<ISearchIndexGateway>().Should().NotBeNull();
        provider.GetService<IApplicationCache>().Should().NotBeNull();
        provider.GetService<IOperationalAuditWriter>().Should().BeOfType<FileSystemOperationalAuditStore>();
        provider.GetService<IOperationalAuditReader>().Should().BeOfType<FileSystemOperationalAuditStore>();

        var options = provider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        options.TimeoutSeconds.Should().Be(12);
    }

    [Fact]
    public async Task ProviderValidation_ShouldFail_WhenRealProvidersAreRequiredButNotConfigured()
    {
        var hostedService = new ProviderConfigurationValidationHostedService(
            Options.Create(new ChatModelOptions { Model = "gpt-4.1" }),
            Options.Create(new EmbeddingOptions { Model = "text-embedding-3-small", Dimensions = 1536 }),
            Options.Create(new OcrOptions { PrimaryProvider = "OllamaVision" }),
            Options.Create(new ProviderExecutionModeOptions
            {
                AllowMockProviders = false,
                AllowInMemoryInfrastructure = false,
                PreferLocalOcr = true
            }),
            Options.Create(new ExternalProviderClientOptions
            {
                TimeoutSeconds = 30
            }));

        var act = async () => await hostedService.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*mocks estao desabilitados*");
    }

    [Fact]
    public void AddInfrastructure_ShouldPreferLocalProviders_WhenConfiguredForOllamaMode()
    {
        var services = new ServiceCollection();
        AddRuntimePrerequisites(services);
        services.Configure<ChatModelOptions>(options => options.Model = "qwen2.5-coder:7b");
        services.Configure<EmbeddingOptions>(options => options.Model = "nomic-embed-text");
        services.Configure<ExternalProviderClientOptions>(options =>
        {
            options.TimeoutSeconds = 30;
            options.OpenAiCompatibleBaseUrl = "http://localhost:11434/v1";
            options.OpenAiCompatibleChatModel = "qwen2.5-coder:7b";
            options.OpenAiCompatibleVisionModel = "llava";
        });
        services.Configure<LocalPersistenceOptions>(options =>
        {
            options.BasePath = Path.Combine(Path.GetTempPath(), "rag-test-local");
        });
        services.Configure<BlobStorageOptions>(_ => { });
        services.Configure<ProviderExecutionModeOptions>(options =>
        {
            options.AllowMockProviders = true;
            options.AllowInMemoryInfrastructure = true;
            options.PreferMockProviders = false;
            options.PreferInMemoryInfrastructure = false;
            options.PreferLocalPersistentInfrastructure = true;
            options.PreferLocalOcr = true;
        });

        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IChatCompletionProvider>().Should().BeOfType<OpenAiCompatibleChatCompletionProvider>();
        provider.GetRequiredService<IOcrProvider>().Should().BeOfType<LocalVisionOcrProvider>();
        provider.GetRequiredService<ISearchIndexGateway>().Should().BeOfType<LocalPersistentSearchIndexGateway>();
        provider.GetRequiredService<IDocumentCatalog>().Should().BeOfType<FileSystemDocumentCatalog>();
    }

    [Fact]
    public void AddInfrastructure_ShouldRegisterRedisStackSearchGateway_WhenConfigured()
    {
        var services = new ServiceCollection();
        AddRuntimePrerequisites(services);
        services.Configure<ExternalProviderClientOptions>(options =>
        {
            options.TimeoutSeconds = 30;
        });
        services.Configure<BlobStorageOptions>(_ => { });
        services.Configure<LocalPersistenceOptions>(options =>
        {
            options.BasePath = Path.Combine(Path.GetTempPath(), "rag-test-redisstack");
        });
        services.Configure<AppCfg.VectorStoreOptions>(options =>
        {
            options.Provider = "redisstack";
            options.ConnectionString = "localhost:6379,abortConnect=false";
            options.IndexName = "idx:rag:test";
            options.KeyPrefix = "rag:test:";
            options.Dimensions = 768;
        });
        services.Configure<RedisSettings>(options =>
        {
            options.Server = "localhost";
            options.Port = 6379;
        });
        services.Configure<AppCfg.RedisCoordinationOptions>(options =>
        {
            options.Enabled = true;
            options.Configuration = "localhost:6379";
            options.KeyPrefix = "chatbot-test";
            options.LockTimeoutSeconds = 30;
        });
        services.Configure<ProviderExecutionModeOptions>(options =>
        {
            options.AllowMockProviders = true;
            options.AllowInMemoryInfrastructure = true;
            options.PreferLocalPersistentInfrastructure = true;
        });

        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISearchIndexGateway>().Should().BeOfType<RedisStackSearchIndexGateway>();
        provider.GetRequiredService<IChatSessionStore>().Should().BeOfType<RedisChatSessionStore>();
        provider.GetRequiredService<IOperationalAuditWriter>().Should().BeOfType<FileSystemOperationalAuditStore>();
        provider.GetRequiredService<IOperationalAuditReader>().Should().BeOfType<FileSystemOperationalAuditStore>();
    }

    private static void AddRuntimePrerequisites(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
    }


}
