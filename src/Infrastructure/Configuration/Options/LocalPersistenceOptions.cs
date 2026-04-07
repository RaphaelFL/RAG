namespace Chatbot.Infrastructure.Configuration;

public sealed class LocalPersistenceOptions
{
    public string BasePath { get; set; } = "artifacts/local-rag";
    public string BlobRootDirectory { get; set; } = "blobs";
    public string DocumentCatalogFileName { get; set; } = "document-catalog.json";
    public string SearchIndexFileName { get; set; } = "search-index.json";
}
