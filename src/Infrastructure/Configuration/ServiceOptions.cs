namespace Chatbot.Infrastructure.Configuration;

public sealed class ChatModelOptions
{
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public int MaxPromptContextTokens { get; set; } = 2800;
    public double TopP { get; set; }
}

public sealed class EmbeddingOptions
{
    public string Model { get; set; } = string.Empty;
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

public sealed class ChunkingOptions
{
    public int DenseChunkSize { get; set; } = 420;
    public int DenseOverlap { get; set; } = 48;
    public int NarrativeChunkSize { get; set; } = 900;
    public int NarrativeOverlap { get; set; } = 96;
    public int MinimumChunkCharacters { get; set; } = 120;
}

public sealed class RetrievalOptimizationOptions
{
    public int CandidateMultiplier { get; set; } = 3;
    public int MaxCandidateCount { get; set; } = 24;
    public int MaxContextChunks { get; set; } = 4;
    public double MinimumRerankScore { get; set; } = 0.2;
    public double ExactMatchBoost { get; set; } = 0.18;
    public double TitleMatchBoost { get; set; } = 0.08;
    public double FilterMatchBoost { get; set; } = 0.05;
}

public sealed class CacheOptions
{
    public int RetrievalTtlSeconds { get; set; } = 300;
    public int ChatCompletionTtlSeconds { get; set; } = 600;
    public int EmbeddingTtlHours { get; set; } = 24;
    public int MaxInMemoryEntries { get; set; } = 2000;
    public string InstancePrefix { get; set; } = "chatbot";
}

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecKey { get; set; } = string.Empty;
    public int TokenExpiresHours { get; set; } = 24;
}

public sealed class CorsPolicyOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public sealed class RedisSettings
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;
}

public sealed class BlobStorageOptions
{
    public string ContainerName { get; set; } = string.Empty;
}

public sealed class LocalPersistenceOptions
{
    public string BasePath { get; set; } = "artifacts/local-rag";
    public string BlobRootDirectory { get; set; } = "blobs";
    public string DocumentCatalogFileName { get; set; } = "document-catalog.json";
    public string SearchIndexFileName { get; set; } = "search-index.json";
}

public sealed class OcrOptions
{
    public string PrimaryProvider { get; set; } = string.Empty;
    public string FallbackProvider { get; set; } = string.Empty;
    public bool EnableFallback { get; set; }
    public bool EnableSelectiveOcr { get; set; } = true;
    public int MinimumDirectTextCharacters { get; set; } = 120;
    public double MinimumDirectTextCoverageRatio { get; set; } = 0.02;
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

public sealed class ProviderExecutionModeOptions
{
    public bool AllowMockProviders { get; set; }
    public bool AllowInMemoryInfrastructure { get; set; }
    public bool PreferMockProviders { get; set; }
    public bool PreferInMemoryInfrastructure { get; set; }
    public bool PreferLocalPersistentInfrastructure { get; set; }
    public bool PreferLocalOcr { get; set; }
}

public sealed class ExternalProviderClientOptions
{
    public int TimeoutSeconds { get; set; }
    public string OpenAiCompatibleBaseUrl { get; set; } = string.Empty;
    public string OpenAiCompatibleApiKey { get; set; } = string.Empty;
    public string OpenAiCompatibleChatModel { get; set; } = string.Empty;
    public string OpenAiCompatibleVisionModel { get; set; } = string.Empty;

    public bool HasOpenAiCompatibleChatConfiguration(string? fallbackModel = null)
    {
        return HasConfiguredValue(OpenAiCompatibleBaseUrl)
            && HasConfiguredValue(ResolveOpenAiCompatibleChatModel(fallbackModel));
    }

    public bool HasOpenAiCompatibleVisionConfiguration()
    {
        return HasConfiguredValue(OpenAiCompatibleBaseUrl)
            && HasConfiguredValue(OpenAiCompatibleVisionModel);
    }

    public string ResolveOpenAiCompatibleChatModel(string? fallbackModel = null)
    {
        return HasConfiguredValue(OpenAiCompatibleChatModel)
            ? OpenAiCompatibleChatModel
            : fallbackModel ?? string.Empty;
    }

    public static bool HasConfiguredValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.Contains("[A PREENCHER]", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("example", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("dummy", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("changeme", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
    }
}
