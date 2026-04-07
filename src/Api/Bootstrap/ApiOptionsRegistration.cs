using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;

namespace Chatbot.Api.Bootstrap;

public static class ApiOptionsRegistration
{
    public static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsPolicyOptions>()
            .Bind(configuration.GetSection("Cors"))
            .Validate(options => options.AllowedOrigins.All(IsAbsoluteOrigin), "Cors:AllowedOrigins deve conter apenas origens absolutas validas.");
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("JWT"));
        services.AddOptions<ChatModelOptions>()
            .Bind(configuration.GetRequiredSection("ChatModelOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "ChatModelOptions:Model e obrigatorio.")
            .Validate(options => options.MaxTokens > 0, "ChatModelOptions:MaxTokens deve ser maior que zero.")
            .Validate(options => options.MaxPromptContextTokens > 0, "ChatModelOptions:MaxPromptContextTokens deve ser maior que zero.")
            .Validate(options => options.TopP is >= 0 and <= 1, "ChatModelOptions:TopP deve estar entre 0 e 1.")
            .ValidateOnStart();
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetRequiredSection("EmbeddingOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "EmbeddingOptions:Model e obrigatorio.")
            .Validate(options => options.Dimensions > 0, "EmbeddingOptions:Dimensions deve ser maior que zero.")
            .Validate(options => options.BatchSize > 0, "EmbeddingOptions:BatchSize deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetRequiredSection("SearchOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.IndexName), "SearchOptions:IndexName e obrigatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SemanticConfigurationName), "SearchOptions:SemanticConfigurationName e obrigatorio.")
            .Validate(options => options.HybridSearchWeight is >= 0 and <= 1, "SearchOptions:HybridSearchWeight deve estar entre 0 e 1.")
            .Validate(options => options.TopK > 0, "SearchOptions:TopK deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<ChunkingOptions>()
            .Bind(configuration.GetSection("ChunkingOptions"))
            .Validate(options => options.DenseChunkSize > 0, "ChunkingOptions:DenseChunkSize deve ser maior que zero.")
            .Validate(options => options.NarrativeChunkSize > 0, "ChunkingOptions:NarrativeChunkSize deve ser maior que zero.")
            .Validate(options => options.DenseOverlap >= 0, "ChunkingOptions:DenseOverlap nao pode ser negativo.")
            .Validate(options => options.NarrativeOverlap >= 0, "ChunkingOptions:NarrativeOverlap nao pode ser negativo.")
            .Validate(options => options.MinimumChunkCharacters > 0, "ChunkingOptions:MinimumChunkCharacters deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<RetrievalOptimizationOptions>()
            .Bind(configuration.GetSection("RetrievalOptimizationOptions"))
            .Validate(options => options.CandidateMultiplier > 0, "RetrievalOptimizationOptions:CandidateMultiplier deve ser maior que zero.")
            .Validate(options => options.MaxCandidateCount > 0, "RetrievalOptimizationOptions:MaxCandidateCount deve ser maior que zero.")
            .Validate(options => options.MaxContextChunks > 0, "RetrievalOptimizationOptions:MaxContextChunks deve ser maior que zero.")
            .Validate(options => options.MinimumRerankScore is >= 0 and <= 1.5, "RetrievalOptimizationOptions:MinimumRerankScore deve estar entre 0 e 1.5.")
            .ValidateOnStart();
        services.AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetRequiredSection("BlobStorageOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName), "BlobStorageOptions:ContainerName e obrigatorio.")
            .ValidateOnStart();
        services.AddOptions<OcrOptions>()
            .Bind(configuration.GetRequiredSection("OcrOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.PrimaryProvider), "OcrOptions:PrimaryProvider e obrigatorio.")
            .Validate(options => !options.EnableFallback || !string.IsNullOrWhiteSpace(options.FallbackProvider), "OcrOptions:FallbackProvider e obrigatorio quando EnableFallback=true.")
            .Validate(options => options.MinimumDirectTextCharacters >= 0, "OcrOptions:MinimumDirectTextCharacters nao pode ser negativo.")
            .Validate(options => options.MinimumDirectTextCoverageRatio is >= 0 and <= 1, "OcrOptions:MinimumDirectTextCoverageRatio deve estar entre 0 e 1.")
            .ValidateOnStart();
        services.AddOptions<PromptTemplateOptions>()
            .Bind(configuration.GetRequiredSection("PromptTemplateOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.GroundedAnswerVersion), "PromptTemplateOptions:GroundedAnswerVersion e obrigatorio.")
            .Validate(options => options.DefaultTimeout > 0, "PromptTemplateOptions:DefaultTimeout deve ser maior que zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.InsufficientEvidenceMessage), "PromptTemplateOptions:InsufficientEvidenceMessage e obrigatorio.")
            .Validate(options => options.BlockedInputPatterns.Length > 0, "PromptTemplateOptions:BlockedInputPatterns deve conter ao menos uma entrada.")
            .ValidateOnStart();
        services.AddOptions<FeatureFlagOptions>()
            .Bind(configuration.GetRequiredSection("FeatureFlagOptions"))
            .ValidateOnStart();
        services.AddOptions<OperationalResilienceOptions>()
            .Bind(configuration.GetSection("OperationalResilienceOptions"))
            .Validate(options => options.TimeoutSeconds > 0, "OperationalResilienceOptions:TimeoutSeconds deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<ProviderExecutionModeOptions>()
            .Bind(configuration.GetSection("ProviderExecutionModeOptions"))
            .Validate(options => !options.PreferMockProviders || options.AllowMockProviders, "ProviderExecutionModeOptions:PreferMockProviders exige AllowMockProviders=true.")
            .Validate(options => !options.PreferInMemoryInfrastructure || options.AllowInMemoryInfrastructure, "ProviderExecutionModeOptions:PreferInMemoryInfrastructure exige AllowInMemoryInfrastructure=true.")
            .Validate(options => !options.PreferLocalPersistentInfrastructure || options.AllowInMemoryInfrastructure, "ProviderExecutionModeOptions:PreferLocalPersistentInfrastructure exige AllowInMemoryInfrastructure=true.")
            .ValidateOnStart();
        services.AddOptions<LocalPersistenceOptions>()
            .Bind(configuration.GetSection("LocalPersistenceOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BasePath), "LocalPersistenceOptions:BasePath e obrigatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BlobRootDirectory), "LocalPersistenceOptions:BlobRootDirectory e obrigatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DocumentCatalogFileName), "LocalPersistenceOptions:DocumentCatalogFileName e obrigatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SearchIndexFileName), "LocalPersistenceOptions:SearchIndexFileName e obrigatorio.")
            .ValidateOnStart();
        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection("CacheOptions"))
            .Validate(options => options.RetrievalTtlSeconds > 0, "CacheOptions:RetrievalTtlSeconds deve ser maior que zero.")
            .Validate(options => options.ChatCompletionTtlSeconds > 0, "CacheOptions:ChatCompletionTtlSeconds deve ser maior que zero.")
            .Validate(options => options.EmbeddingTtlHours > 0, "CacheOptions:EmbeddingTtlHours deve ser maior que zero.")
            .Validate(options => options.MaxInMemoryEntries > 0, "CacheOptions:MaxInMemoryEntries deve ser maior que zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.InstancePrefix), "CacheOptions:InstancePrefix e obrigatorio.")
            .ValidateOnStart();
        services.AddOptions<RedisSettings>()
            .Bind(configuration.GetSection("RedisSettings"))
            .Validate(options => options.Port > 0, "RedisSettings:Port deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<ExternalProviderClientOptions>()
            .Bind(configuration.GetRequiredSection("ExternalProviderClientOptions"))
            .Validate(options => options.TimeoutSeconds > 0, "ExternalProviderClientOptions:TimeoutSeconds deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<EmbeddingGenerationOptions>()
            .Bind(configuration.GetSection("EmbeddingGenerationOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ModelName), "EmbeddingGenerationOptions:ModelName e obrigatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ModelVersion), "EmbeddingGenerationOptions:ModelVersion e obrigatorio.")
            .Validate(options => options.BatchSize > 0, "EmbeddingGenerationOptions:BatchSize deve ser maior que zero.")
            .Validate(options => options.Dimensions > 0, "EmbeddingGenerationOptions:Dimensions deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<VectorStoreOptions>()
            .Bind(configuration.GetSection("VectorStoreOptions"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider), "VectorStoreOptions:Provider e obrigatorio.")
            .Validate(options => options.DefaultTopK > 0, "VectorStoreOptions:DefaultTopK deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<RedisCoordinationOptions>()
            .Bind(configuration.GetSection("RedisCoordinationOptions"))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.KeyPrefix), "RedisCoordinationOptions:KeyPrefix e obrigatorio quando habilitado.")
            .ValidateOnStart();
        services.AddOptions<AgentRuntimeOptions>()
            .Bind(configuration.GetSection("AgentRuntimeOptions"))
            .Validate(options => options.MaxToolBudget > 0, "AgentRuntimeOptions:MaxToolBudget deve ser maior que zero.")
            .Validate(options => options.MaxDepth > 0, "AgentRuntimeOptions:MaxDepth deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<CodeInterpreterOptions>()
            .Bind(configuration.GetSection("CodeInterpreterOptions"))
            .Validate(options => options.TimeoutSeconds > 0, "CodeInterpreterOptions:TimeoutSeconds deve ser maior que zero.")
            .Validate(options => options.MemoryLimitMb > 0, "CodeInterpreterOptions:MemoryLimitMb deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<WebSearchOptions>()
            .Bind(configuration.GetSection("WebSearchOptions"))
            .Validate(options => options.DefaultTopK > 0, "WebSearchOptions:DefaultTopK deve ser maior que zero.")
            .Validate(options => options.TimeoutSeconds > 0, "WebSearchOptions:TimeoutSeconds deve ser maior que zero.")
            .ValidateOnStart();
        services.AddOptions<StructuredExtractionOptions>()
            .Bind(configuration.GetSection("StructuredExtractionOptions"))
            .ValidateOnStart();

        return services;
    }

    private static bool IsAbsoluteOrigin(string? origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }
}