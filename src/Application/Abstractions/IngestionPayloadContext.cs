namespace Chatbot.Application.Abstractions;

public sealed class IngestionPayloadContext
{
    public required Guid DocumentId { get; init; }

    public required byte[] Payload { get; init; }

    public required string RawHash { get; init; }

    public required string StoragePath { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required int Version { get; init; }

    public required int IndexedChunkCount { get; init; }
}