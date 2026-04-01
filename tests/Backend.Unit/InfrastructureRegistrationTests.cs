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
            options.AzureOpenAiBaseUrl = "https://aoai.example/";
            options.AzureOpenAiApiKey = "aoai-key";
            options.AzureOpenAiChatDeployment = "chat-deployment";
            options.AzureOpenAiEmbeddingDeployment = "embedding-deployment";
            options.AzureSearchBaseUrl = "https://search.example/";
            options.AzureSearchApiKey = "search-key";
            options.BlobStorageBaseUrl = "https://blob.example/";
            options.AzureDocumentIntelligenceBaseUrl = "https://di.example/";
            options.AzureDocumentIntelligenceApiKey = "di-key";
            options.GoogleVisionBaseUrl = "https://vision.example/";
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

        var options = provider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        options.TimeoutSeconds.Should().Be(12);
    }
}