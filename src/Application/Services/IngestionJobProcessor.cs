using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

public sealed class IngestionJobProcessor : IIngestionJobProcessor
{
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IBlobStorageGateway _blobGateway;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<IngestionJobProcessor> _logger;

    public IngestionJobProcessor(
        IDocumentTextExtractor documentTextExtractor,
        IEmbeddingProvider embeddingProvider,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IBlobStorageGateway blobGateway,
        IPromptInjectionDetector promptInjectionDetector,
        ISecurityAuditLogger securityAuditLogger,
        ResiliencePipeline resiliencePipeline,
        ILogger<IngestionJobProcessor> logger)
    {
        _documentTextExtractor = documentTextExtractor;
        _embeddingProvider = embeddingProvider;
        _indexGateway = indexGateway;
        _chunkingStrategy = chunkingStrategy;
        _documentCatalog = documentCatalog;
        _blobGateway = blobGateway;
        _promptInjectionDetector = promptInjectionDetector;
        _securityAuditLogger = securityAuditLogger;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task ProcessIngestionAsync(IngestionBackgroundJob job, CancellationToken ct)
    {
        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("ingestion.process");
            activity?.SetTag("document.id", job.DocumentId);
            var startedAt = DateTime.UtcNow;
            UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Parsing, job.JobId);

            var extracted = await _resiliencePipeline.ExecuteAsync(async token =>
                await _documentTextExtractor.ExtractAsync(ToCommand(job), token), ct);
            if (string.Equals(extracted.Strategy, "ocr", StringComparison.OrdinalIgnoreCase))
            {
                UpdateDocumentStatus(job.DocumentId, DocumentStatuses.OcrProcessing, job.JobId);
            }

            var extraction = EnsureExtractionHasContent(extracted, job.FileName);
            var normalizedText = extraction.Text;

            if (_promptInjectionDetector.TryDetect(normalizedText, out var pattern))
            {
                _securityAuditLogger.LogPromptInjectionDetected($"document:{job.DocumentId}", $"Matched blocked pattern '{pattern}'.");
                ChatbotTelemetry.PromptInjectionSignals.Add(1);
            }

            UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Chunking, job.JobId);
            var chunks = _chunkingStrategy.Chunk(ToCommand(job), extraction);
            UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
            await EnrichChunksAsync(chunks, null, false, ct);

            UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
                await _indexGateway.IndexDocumentChunksAsync(chunks, token);
            }, ct);

            var document = _documentCatalog.Get(job.DocumentId);
            if (document is null)
            {
                return;
            }

            document.Status = DocumentStatuses.Indexed;
            document.StoragePath = job.StoragePath;
            document.QuarantinePath = null;
            document.ContentHash = job.RawHash;
            document.OriginalFileName = job.FileName;
            document.IndexedChunkCount = chunks.Count;
            document.Chunks = new List<DocumentChunkIndexDto>();
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.LastJobId = job.JobId;
            _documentCatalog.Upsert(document);
            ChatbotTelemetry.IngestionLatencyMs.Record((DateTime.UtcNow - startedAt).TotalMilliseconds);

            _logger.LogInformation("Document {documentId} indexed with {chunkCount} chunks", job.DocumentId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting document {documentId}", job.DocumentId);
            var failed = _documentCatalog.Get(job.DocumentId);
            if (failed is not null)
            {
                failed.Status = DocumentStatuses.Failed;
                failed.UpdatedAtUtc = DateTime.UtcNow;
                failed.LastJobId = job.JobId;
                _documentCatalog.Upsert(failed);
            }
        }
    }

    public async Task ProcessReindexAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("reindex.process");
            activity?.SetTag("document.id", job.DocumentId);
            if (job.FullReindex)
            {
                await ProcessFullReindexAsync(job, ct);
                return;
            }

            await Task.Delay(job.FullReindex ? 150 : 75, ct);
            var document = _documentCatalog.Get(job.DocumentId);
            if (document is null)
            {
                return;
            }

            var indexedChunks = await _indexGateway.GetDocumentChunksAsync(job.DocumentId, ct);
            if (indexedChunks.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(document.StoragePath))
                {
                    await ProcessFullReindexAsync(job, ct);
                    return;
                }

                throw new InvalidOperationException($"No persisted chunks were found for document {job.DocumentId}.");
            }

            var updatedEmbeddings = 0;
            UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
            foreach (var chunk in indexedChunks)
            {
                if (chunk.Embedding is { Length: > 0 } && string.IsNullOrWhiteSpace(job.ForceEmbeddingModel))
                {
                    ChatbotTelemetry.EmbeddingReuse.Add(1, new KeyValuePair<string, object?>("reuse.kind", "existing-chunk"));
                    continue;
                }

                chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, job.ForceEmbeddingModel, token), ct);
                chunk.Metadata["embeddingModel"] = string.IsNullOrWhiteSpace(job.ForceEmbeddingModel) ? "default" : job.ForceEmbeddingModel;
                chunk.Metadata["contentHash"] = ComputeHash(chunk.Content);
                updatedEmbeddings++;
            }

            if (updatedEmbeddings > 0)
            {
                UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
                await _resiliencePipeline.ExecuteAsync(async token =>
                    await _indexGateway.IndexDocumentChunksAsync(indexedChunks, token), ct);
            }

            document.Version += 1;
            document.Status = DocumentStatuses.Indexed;
            document.IndexedChunkCount = indexedChunks.Count;
            document.Chunks = new List<DocumentChunkIndexDto>();
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.LastJobId = job.JobId;
            _documentCatalog.Upsert(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reindexing document {documentId}", job.DocumentId);
            var document = _documentCatalog.Get(job.DocumentId);
            if (document is not null)
            {
                document.Status = DocumentStatuses.Failed;
                document.UpdatedAtUtc = DateTime.UtcNow;
                _documentCatalog.Upsert(document);
            }
        }
    }

    private async Task ProcessFullReindexAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        var document = _documentCatalog.Get(job.DocumentId);
        if (document is null || string.IsNullOrWhiteSpace(document.StoragePath))
        {
            return;
        }

        UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Parsing, job.JobId);
        await using var content = await _blobGateway.GetAsync(document.StoragePath, ct);
        var payload = await ReadContentAsync(content, ct);
        var command = BuildReindexCommand(document, payload);
        var extracted = await _resiliencePipeline.ExecuteAsync(async token =>
            await _documentTextExtractor.ExtractAsync(command, token), ct);
        if (string.Equals(extracted.Strategy, "ocr", StringComparison.OrdinalIgnoreCase))
        {
            UpdateDocumentStatus(job.DocumentId, DocumentStatuses.OcrProcessing, job.JobId);
        }
        var extraction = EnsureExtractionHasContent(extracted, command.FileName);
        var normalizedText = extraction.Text;

        if (_promptInjectionDetector.TryDetect(normalizedText, out var pattern))
        {
            _securityAuditLogger.LogPromptInjectionDetected($"document:{job.DocumentId}", $"Matched blocked pattern '{pattern}'.");
            ChatbotTelemetry.PromptInjectionSignals.Add(1);
        }

        UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Chunking, job.JobId);
        var chunks = _chunkingStrategy.Chunk(command, extraction);
        UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
        await EnrichChunksAsync(chunks, job.ForceEmbeddingModel, false, ct);

        UpdateDocumentStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
            await _indexGateway.IndexDocumentChunksAsync(chunks, token);
        }, ct);

        document.Version += 1;
        document.Status = DocumentStatuses.Indexed;
        document.ContentHash = ComputeHash(payload);
        document.ContentType = command.ContentType;
        document.OriginalFileName = command.FileName;
        document.IndexedChunkCount = chunks.Count;
        document.Chunks = new List<DocumentChunkIndexDto>();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = job.JobId;
        _documentCatalog.Upsert(document);
    }

    private static IngestDocumentCommand ToCommand(IngestionBackgroundJob job)
    {
        return new IngestDocumentCommand
        {
            DocumentId = job.DocumentId,
            TenantId = job.TenantId,
            FileName = job.FileName,
            ContentType = job.ContentType,
            ContentLength = job.ContentLength,
            DocumentTitle = job.DocumentTitle,
            Category = job.Category,
            Tags = new List<string>(job.Tags),
            Categories = new List<string>(job.Categories),
            Source = job.Source,
            ExternalId = job.ExternalId,
            AccessPolicy = job.AccessPolicy,
            Content = new MemoryStream(job.Payload, writable: false)
        };
    }

    private void UpdateDocumentStatus(Guid documentId, string status, Guid jobId)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return;
        }

        document.Status = status;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);
    }

    private async Task EnrichChunksAsync(
        List<DocumentChunkIndexDto> chunks,
        string? forceEmbeddingModel,
        bool forceRefresh,
        CancellationToken ct)
    {
        foreach (var chunk in chunks)
        {
            chunk.Metadata["contentHash"] = ComputeHash(chunk.Content);
            if (!forceRefresh && chunk.Embedding is { Length: > 0 } && string.IsNullOrWhiteSpace(forceEmbeddingModel))
            {
                ChatbotTelemetry.EmbeddingReuse.Add(1, new KeyValuePair<string, object?>("reuse.kind", "existing-chunk"));
                continue;
            }

            chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, forceEmbeddingModel, token), ct);
            chunk.Metadata["embeddingModel"] = string.IsNullOrWhiteSpace(forceEmbeddingModel) ? "default" : forceEmbeddingModel;
        }
    }

    private static IngestDocumentCommand BuildReindexCommand(DocumentCatalogEntry document, byte[] payload)
    {
        var fileName = string.IsNullOrWhiteSpace(document.OriginalFileName)
            ? BuildFallbackFileName(document)
            : document.OriginalFileName;

        return new IngestDocumentCommand
        {
            DocumentId = document.DocumentId,
            TenantId = document.TenantId,
            FileName = fileName,
            ContentType = document.ContentType,
            ContentLength = payload.LongLength,
            DocumentTitle = document.Title,
            Category = document.Category,
            Tags = new List<string>(document.Tags),
            Categories = new List<string>(document.Categories),
            Source = document.Source,
            ExternalId = document.ExternalId,
            AccessPolicy = document.AccessPolicy,
            Content = new MemoryStream(payload, writable: false)
        };
    }

    private static string BuildFallbackFileName(DocumentCatalogEntry document)
    {
        var extension = document.ContentType switch
        {
            "application/pdf" => ".pdf",
            "text/plain" => ".txt",
            "text/markdown" => ".md",
            "text/html" => ".html",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            _ => ".bin"
        };

        return $"{document.Title}{extension}";
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

    private static DocumentTextExtractionResultDto EnsureExtractionHasContent(DocumentTextExtractionResultDto extracted, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(extracted.Text))
        {
            return extracted;
        }

        var fallbackText = $"Conteudo indisponivel para {fileName}";
        return new DocumentTextExtractionResultDto
        {
            Text = fallbackText,
            Strategy = extracted.Strategy,
            Provider = extracted.Provider,
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = fallbackText
                }
            }
        };
    }

    private static int ParseMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : 1;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content));
    }
}
