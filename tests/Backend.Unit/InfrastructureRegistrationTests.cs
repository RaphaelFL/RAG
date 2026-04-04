using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Refit;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_ShouldRegisterTypedClientsAndProviderOptions()
    {
        var services = new ServiceCollection();
        AddRuntimePrerequisites(services);
        services.Configure<ExternalProviderClientOptions>(options =>
        {
            options.TimeoutSeconds = 12;
            options.AzureOpenAiBaseUrl = "https://aoai.contoso.local/";
            options.AzureOpenAiApiKey = "aoai-key";
            options.AzureOpenAiChatDeployment = "chat-deployment";
            options.AzureOpenAiEmbeddingDeployment = "embedding-deployment";
            options.AzureSearchBaseUrl = "https://search.contoso.local/";
            options.AzureSearchApiKey = "search-key";
            options.BlobStorageBaseUrl = "https://blob.contoso.local/";
            options.AzureDocumentIntelligenceBaseUrl = "https://di.contoso.local/";
            options.AzureDocumentIntelligenceApiKey = "di-key";
            options.GoogleVisionBaseUrl = "https://vision.contoso.local/";
            options.GoogleVisionApiKey = "vision-key";
            options.AzureOpenAiApiVersion = "2024-10-21";
            options.AzureSearchApiVersion = "2024-07-01";
            options.AzureDocumentIntelligenceApiVersion = "2024-11-30";
        });
        services.Configure<BlobStorageOptions>(options =>
        {
            options.ConnectionString = "[A PREENCHER]";
        });
        services.Configure<LocalPersistenceOptions>(options =>
        {
            options.BasePath = Path.Combine(Path.GetTempPath(), "rag-test-registration");
        });

        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IAzureOpenAiClient>().Should().NotBeNull();
        provider.GetService<IAzureSearchClient>().Should().NotBeNull();
        provider.GetService<IBlobStorageClient>().Should().NotBeNull();
        provider.GetService<IGoogleVisionClient>().Should().NotBeNull();
        provider.GetService<IChatCompletionProvider>().Should().NotBeNull();
        provider.GetService<IEmbeddingProvider>().Should().NotBeNull();
        provider.GetService<IOcrProvider>().Should().NotBeNull();
        provider.GetService<ISearchIndexGateway>().Should().NotBeNull();
        provider.GetService<IApplicationCache>().Should().NotBeNull();

        var options = provider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        options.TimeoutSeconds.Should().Be(12);
    }

    [Fact]
    public async Task ProviderValidation_ShouldFail_WhenRealProvidersAreRequiredButNotConfigured()
    {
        var hostedService = new ProviderConfigurationValidationHostedService(
            Options.Create(new ChatModelOptions { Model = "gpt-4.1" }),
            Options.Create(new EmbeddingOptions { Model = "text-embedding-3-small", Dimensions = 1536 }),
            Options.Create(new SearchOptions { IndexName = "chatbot-documents" }),
            Options.Create(new BlobStorageOptions { ContainerName = "documents" }),
            Options.Create(new OcrOptions { PrimaryProvider = "AzureDocumentIntelligence", AzureDocumentIntelligenceModelId = "prebuilt-read" }),
            Options.Create(new ProviderExecutionModeOptions
            {
                AllowMockProviders = false,
                AllowInMemoryInfrastructure = false
            }),
            Options.Create(new ExternalProviderClientOptions
            {
                TimeoutSeconds = 30,
                AzureOpenAiApiVersion = "2024-10-21",
                AzureSearchApiVersion = "2024-07-01",
                AzureDocumentIntelligenceApiVersion = "2024-11-30"
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
            options.OpenAiCompatibleEmbeddingModel = "nomic-embed-text";
            options.OpenAiCompatibleVisionModel = "llava";
            options.AzureOpenAiApiVersion = "2024-10-21";
            options.AzureSearchApiVersion = "2024-07-01";
            options.AzureDocumentIntelligenceApiVersion = "2024-11-30";
            options.AzureSearchBaseUrl = "https://search.contoso.local/";
            options.BlobStorageBaseUrl = "https://blob.contoso.local/";
            options.AzureDocumentIntelligenceBaseUrl = "https://di.contoso.local/";
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

    private static void AddRuntimePrerequisites(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Backend.Unit";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

}