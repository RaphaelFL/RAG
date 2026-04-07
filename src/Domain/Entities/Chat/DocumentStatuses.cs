namespace Chatbot.Domain.Entities;

public static class DocumentStatuses
{
    public const string Uploaded = "Uploaded";
    public const string Queued = "Queued";
    public const string Parsing = "Parsing";
    public const string OcrProcessing = "OcrProcessing";
    public const string Chunking = "Chunking";
    public const string Embedding = "Embedding";
    public const string Indexing = "Indexing";
    public const string Indexed = "Indexed";
    public const string ReindexPending = "ReindexPending";
    public const string Failed = "Failed";
    public const string Archived = "Archived";
    public const string Deleted = "Deleted";
}