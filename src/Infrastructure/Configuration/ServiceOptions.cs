namespace Chatbot.Infrastructure.Configuration;

public sealed class ChatModelOptions
{
    public string Model { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public double TopP { get; set; }
}

public sealed class EmbeddingOptions
{
    public string Model { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public int BatchSize { get; set; }
}

public sealed class SearchOptions
{
    public string IndexName { get; set; } = string.Empty;
    public string SemanticConfigurationName { get; set; } = string.Empty;
    public double HybridSearchWeight { get; set; }
    public bool SemanticRankingEnabled { get; set; }
    public int TopK { get; set; }
}

public sealed class BlobStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
}

public sealed class OcrOptions
{
    public string PrimaryProvider { get; set; } = string.Empty;
    public string FallbackProvider { get; set; } = string.Empty;
    public string AzureDocumentIntelligenceModelId { get; set; } = string.Empty;
    public bool EnableFallback { get; set; }
}

public sealed class PromptTemplateOptions
{
    public string GroundedAnswerVersion { get; set; } = string.Empty;
    public int DefaultTimeout { get; set; }
    public string InsufficientEvidenceMessage { get; set; } = string.Empty;
    public string[] BlockedInputPatterns { get; set; } = Array.Empty<string>();
}

public sealed class FeatureFlagOptions
{
    public bool EnableSemanticRanking { get; set; }
    public bool EnableMcp { get; set; }
    public bool EnableGraphRag { get; set; }
    public bool EnableRedisCache { get; set; }
}

public sealed class ExternalProviderClientOptions
{
    public int TimeoutSeconds { get; set; }
    public string AzureOpenAiBaseUrl { get; set; } = string.Empty;
    public string AzureOpenAiApiKey { get; set; } = string.Empty;
    public string AzureOpenAiApiVersion { get; set; } = string.Empty;
    public string AzureOpenAiChatDeployment { get; set; } = string.Empty;
    public string AzureOpenAiEmbeddingDeployment { get; set; } = string.Empty;
    public string AzureSearchBaseUrl { get; set; } = string.Empty;
    public string AzureSearchApiKey { get; set; } = string.Empty;
    public string AzureSearchApiVersion { get; set; } = string.Empty;
    public string BlobStorageBaseUrl { get; set; } = string.Empty;
    public string AzureDocumentIntelligenceBaseUrl { get; set; } = string.Empty;
    public string AzureDocumentIntelligenceApiKey { get; set; } = string.Empty;
    public string AzureDocumentIntelligenceApiVersion { get; set; } = string.Empty;
    public string GoogleVisionBaseUrl { get; set; } = string.Empty;
    public string GoogleVisionApiKey { get; set; } = string.Empty;

    public bool HasAzureOpenAiChatConfiguration()
    {
        return HasConfiguredValue(AzureOpenAiBaseUrl)
            && HasConfiguredValue(AzureOpenAiApiKey)
            && HasConfiguredValue(AzureOpenAiChatDeployment);
    }

    public bool HasAzureOpenAiEmbeddingConfiguration()
    {
        return HasConfiguredValue(AzureOpenAiBaseUrl)
            && HasConfiguredValue(AzureOpenAiApiKey)
            && HasConfiguredValue(AzureOpenAiEmbeddingDeployment);
    }

    public bool HasAzureSearchConfiguration()
    {
        return HasConfiguredValue(AzureSearchBaseUrl)
            && HasConfiguredValue(AzureSearchApiKey);
    }

    public bool HasAzureDocumentIntelligenceConfiguration()
    {
        return HasConfiguredValue(AzureDocumentIntelligenceBaseUrl)
            && HasConfiguredValue(AzureDocumentIntelligenceApiKey);
    }

    public bool HasGoogleVisionConfiguration()
    {
        return HasConfiguredValue(GoogleVisionBaseUrl)
            && HasConfiguredValue(GoogleVisionApiKey);
    }

    public static bool HasConfiguredValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.Contains("[A PREENCHER]", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("example", StringComparison.OrdinalIgnoreCase);
    }
}
