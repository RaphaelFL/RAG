namespace Chatbot.Application.Abstractions;

public sealed class DocumentFileReference
{
    public required Guid DocumentId { get; init; }

    public required string OriginalFileName { get; init; }

    public required string ContentType { get; init; }

    public required string StoragePath { get; init; }
}