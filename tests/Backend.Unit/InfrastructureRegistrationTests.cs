using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Refit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_ShouldRegisterTypedClientsAndProviderOptions()
    {
        var services = new ServiceCollection();
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
        });
        services.Configure<BlobStorageOptions>(options =>
        {
            options.ConnectionString = "[A PREENCHER]";
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
}