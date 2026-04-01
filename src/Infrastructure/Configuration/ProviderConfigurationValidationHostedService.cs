using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Configuration;

public sealed class ProviderConfigurationValidationHostedService : IHostedService
{
    private readonly ChatModelOptions _chatOptions;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly SearchOptions _searchOptions;
    private readonly BlobStorageOptions _blobOptions;
    private readonly OcrOptions _ocrOptions;
    private readonly ExternalProviderClientOptions _providerOptions;

    public ProviderConfigurationValidationHostedService(
        IOptions<ChatModelOptions> chatOptions,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<SearchOptions> searchOptions,
        IOptions<BlobStorageOptions> blobOptions,
        IOptions<OcrOptions> ocrOptions,
        IOptions<ExternalProviderClientOptions> providerOptions)
    {
        _chatOptions = chatOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _searchOptions = searchOptions.Value;
        _blobOptions = blobOptions.Value;
        _ocrOptions = ocrOptions.Value;
        _providerOptions = providerOptions.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        ValidateAzureOpenAiChat(errors);
        ValidateAzureOpenAiEmbeddings(errors);
        ValidateAzureSearch(errors);
        ValidateBlobStorage(errors);
        ValidateAzureDocumentIntelligence(errors);
        ValidateGoogleVision(errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Configuracao invalida de providers externos:\n- " + string.Join("\n- ", errors));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateAzureOpenAiChat(List<string> errors)
    {
        var fields = new[]
        {
            _providerOptions.AzureOpenAiBaseUrl,
            _providerOptions.AzureOpenAiApiKey,
            _providerOptions.AzureOpenAiChatDeployment,
            _chatOptions.Deployment
        };

        if (!HasAnyConfiguredValue(fields))
        {
            return;
        }

        if (!ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureOpenAiBaseUrl)
            || !ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureOpenAiApiKey)
            || !HasAnyConfiguredValue(new[] { _providerOptions.AzureOpenAiChatDeployment, _chatOptions.Deployment }))
        {
            errors.Add("Azure OpenAI chat esta parcialmente configurado. Preencha ExternalProviderClientOptions:AzureOpenAiBaseUrl, ExternalProviderClientOptions:AzureOpenAiApiKey e ExternalProviderClientOptions:AzureOpenAiChatDeployment ou ChatModelOptions:Deployment.");
        }
    }

    private void ValidateAzureOpenAiEmbeddings(List<string> errors)
    {
        var fields = new[]
        {
            _providerOptions.AzureOpenAiBaseUrl,
            _providerOptions.AzureOpenAiApiKey,
            _providerOptions.AzureOpenAiEmbeddingDeployment,
            _embeddingOptions.Deployment
        };

        if (!HasAnyConfiguredValue(fields))
        {
            return;
        }

        if (!ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureOpenAiBaseUrl)
            || !ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureOpenAiApiKey)
            || !HasAnyConfiguredValue(new[] { _providerOptions.AzureOpenAiEmbeddingDeployment, _embeddingOptions.Deployment }))
        {
            errors.Add("Azure OpenAI embeddings esta parcialmente configurado. Preencha ExternalProviderClientOptions:AzureOpenAiBaseUrl, ExternalProviderClientOptions:AzureOpenAiApiKey e ExternalProviderClientOptions:AzureOpenAiEmbeddingDeployment ou EmbeddingOptions:Deployment.");
        }
    }

    private void ValidateAzureSearch(List<string> errors)
    {
        var fields = new[]
        {
            _providerOptions.AzureSearchBaseUrl,
            _providerOptions.AzureSearchApiKey,
            _searchOptions.IndexName
        };

        if (!HasAnyConfiguredValue(fields))
        {
            return;
        }

        if (!ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureSearchBaseUrl)
            || !ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureSearchApiKey)
            || string.IsNullOrWhiteSpace(_searchOptions.IndexName))
        {
            errors.Add("Azure AI Search esta parcialmente configurado. Preencha ExternalProviderClientOptions:AzureSearchBaseUrl, ExternalProviderClientOptions:AzureSearchApiKey e SearchOptions:IndexName.");
        }
    }

    private void ValidateBlobStorage(List<string> errors)
    {
        var connectionConfigured = ExternalProviderClientOptions.HasConfiguredValue(_blobOptions.ConnectionString);
        var containerConfigured = !string.IsNullOrWhiteSpace(_blobOptions.ContainerName);
        if (!connectionConfigured && containerConfigured)
        {
            return;
        }

        if (connectionConfigured && !containerConfigured)
        {
            errors.Add("Azure Blob Storage esta parcialmente configurado. Preencha BlobStorageOptions:ConnectionString e BlobStorageOptions:ContainerName.");
        }
    }

    private void ValidateAzureDocumentIntelligence(List<string> errors)
    {
        var fields = new[]
        {
            _providerOptions.AzureDocumentIntelligenceBaseUrl,
            _providerOptions.AzureDocumentIntelligenceApiKey,
            _ocrOptions.AzureDocumentIntelligenceModelId
        };

        if (!HasAnyConfiguredValue(fields))
        {
            return;
        }

        if (!ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureDocumentIntelligenceBaseUrl)
            || !ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureDocumentIntelligenceApiKey)
            || string.IsNullOrWhiteSpace(_ocrOptions.AzureDocumentIntelligenceModelId))
        {
            errors.Add("Azure Document Intelligence esta parcialmente configurado. Preencha ExternalProviderClientOptions:AzureDocumentIntelligenceBaseUrl, ExternalProviderClientOptions:AzureDocumentIntelligenceApiKey e OcrOptions:AzureDocumentIntelligenceModelId.");
        }
    }

    private void ValidateGoogleVision(List<string> errors)
    {
        var fields = new[]
        {
            _providerOptions.GoogleVisionBaseUrl,
            _providerOptions.GoogleVisionApiKey
        };

        if (!HasAnyConfiguredValue(fields))
        {
            return;
        }

        if (!ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.GoogleVisionBaseUrl)
            || !ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.GoogleVisionApiKey))
        {
            errors.Add("Google Vision esta parcialmente configurado. Preencha ExternalProviderClientOptions:GoogleVisionBaseUrl e ExternalProviderClientOptions:GoogleVisionApiKey.");
        }
    }

    private static bool HasAnyConfiguredValue(IEnumerable<string?> values)
    {
        return values.Any(ExternalProviderClientOptions.HasConfiguredValue);
    }
}
