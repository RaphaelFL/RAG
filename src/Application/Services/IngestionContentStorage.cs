using System.Security.Cryptography;
using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

public sealed class IngestionContentStorage : IIngestionContentStorage
{
    private readonly IBlobStorageGateway _blobGateway;
    private readonly IDocumentCatalog _documentCatalog;

    public IngestionContentStorage(IBlobStorageGateway blobGateway, IDocumentCatalog documentCatalog)
    {
        _blobGateway = blobGateway;
        _documentCatalog = documentCatalog;
    }

    public async Task<IngestionPayloadContext> PrepareAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        var payload = await ReadContentAsync(command.Content, ct);
        var rawHash = ComputeHash(payload);
        var duplicate = _documentCatalog.FindByContentHash(command.TenantId, rawHash);
        var existingFailedDocument = duplicate is not null && CanRetryFailedDuplicate(duplicate)
            ? duplicate
            : null;

        if (duplicate is not null && existingFailedDocument is null)
        {
            throw new DuplicateDocumentException(
                $"A document with the same content already exists for this tenant: {duplicate.DocumentId}",
                duplicate.DocumentId);
        }

        var documentId = existingFailedDocument?.DocumentId ?? command.DocumentId;
        var storagePath = $"documents/{command.TenantId}/{documentId}/raw-content";
        await _blobGateway.SaveAsync(new MemoryStream(payload, writable: false), storagePath, ct);

        return new IngestionPayloadContext
        {
            DocumentId = documentId,
            Payload = payload,
            RawHash = rawHash,
            StoragePath = storagePath,
            CreatedAtUtc = existingFailedDocument?.CreatedAtUtc ?? DateTime.UtcNow,
            Version = existingFailedDocument?.Version ?? 1,
            IndexedChunkCount = existingFailedDocument?.IndexedChunkCount ?? 0
        };
    }

    public async Task<string?> SaveQuarantineAsync(IngestDocumentCommand command, IngestionPayloadContext context, bool requiresQuarantine, CancellationToken ct)
    {
        if (!requiresQuarantine)
        {
            return null;
        }

        var quarantinePath = $"quarantine/{command.TenantId}/{context.DocumentId}/{command.FileName}";
        await _blobGateway.SaveAsync(new MemoryStream(context.Payload, writable: false), quarantinePath, ct);
        return quarantinePath;
    }

    private static async Task<byte[]> ReadContentAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return buffer.ToArray();
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content));
    }

    private static bool CanRetryFailedDuplicate(DocumentCatalogEntry document)
    {
        return string.Equals(document.Status, "Failed", StringComparison.OrdinalIgnoreCase);
    }
}