namespace Chatbot.Infrastructure.Configuration;

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