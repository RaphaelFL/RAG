using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace Chatbot.Application.Services;

public sealed class IngestionJobProcessor : IIngestionJobProcessor
{
    private readonly IIngestionCommandFactory _ingestionCommandFactory;
    private readonly IIngestionExtractionService _ingestionExtractionService;
    private readonly IIngestionChunkEnricher _ingestionChunkEnricher;
    private readonly IIngestionDocumentStateService _ingestionDocumentStateService;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IBlobStorageGateway _blobGateway;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<IngestionJobProcessor> _logger;

    public IngestionJobProcessor(
        IIngestionCommandFactory ingestionCommandFactory,
        IIngestionExtractionService ingestionExtractionService,
        IIngestionChunkEnricher ingestionChunkEnricher,
        IIngestionDocumentStateService ingestionDocumentStateService,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IBlobStorageGateway blobGateway,
        ResiliencePipeline resiliencePipeline,
        ILogger<IngestionJobProcessor> logger)
    {
        _ingestionCommandFactory = ingestionCommandFactory;
        _ingestionExtractionService = ingestionExtractionService;
        _ingestionChunkEnricher = ingestionChunkEnricher;
        _ingestionDocumentStateService = ingestionDocumentStateService;
        _indexGateway = indexGateway;
        _chunkingStrategy = chunkingStrategy;
        _documentCatalog = documentCatalog;
        _blobGateway = blobGateway;
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
            var command = _ingestionCommandFactory.Create(job);
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Parsing, job.JobId);

            var extracted = await _ingestionExtractionService.ExtractAsync(job.DocumentId, command, ct);
            if (string.Equals(extracted.Strategy, "ocr", StringComparison.OrdinalIgnoreCase))
            {
                _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.OcrProcessing, job.JobId);
            }

            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Chunking, job.JobId);
            var chunks = _chunkingStrategy.Chunk(command, extracted);
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
            await _ingestionChunkEnricher.EnrichAsync(chunks, null, false, ct);

            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
                await _indexGateway.IndexDocumentChunksAsync(chunks, token);
            }, ct);

            _ingestionDocumentStateService.CompleteIngestion(job, chunks.Count);
            ChatbotTelemetry.IngestionLatencyMs.Record((DateTime.UtcNow - startedAt).TotalMilliseconds);

            _logger.LogInformation("Document {documentId} indexed with {chunkCount} chunks", job.DocumentId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting document {documentId}", job.DocumentId);
            _ingestionDocumentStateService.MarkIngestionFailure(job.DocumentId, job.JobId);
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
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
            updatedEmbeddings = await _ingestionChunkEnricher.EnrichAsync(indexedChunks, job.ForceEmbeddingModel, false, ct);

            if (updatedEmbeddings > 0)
            {
                _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
                await _resiliencePipeline.ExecuteAsync(async token =>
                    await _indexGateway.IndexDocumentChunksAsync(indexedChunks, token), ct);
            }

            _ingestionDocumentStateService.CompleteReindex(document, job.JobId, indexedChunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reindexing document {documentId}", job.DocumentId);
            _ingestionDocumentStateService.MarkReindexFailure(job.DocumentId);
        }
    }

    private async Task ProcessFullReindexAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        var document = _documentCatalog.Get(job.DocumentId);
        if (document is null || string.IsNullOrWhiteSpace(document.StoragePath))
        {
            return;
        }

        _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Parsing, job.JobId);
        await using var content = await _blobGateway.GetAsync(document.StoragePath, ct);
        var payload = await ReadContentAsync(content, ct);
        var command = _ingestionCommandFactory.Create(document, payload);
        var extracted = await _ingestionExtractionService.ExtractAsync(job.DocumentId, command, ct);
        if (string.Equals(extracted.Strategy, "ocr", StringComparison.OrdinalIgnoreCase))
        {
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.OcrProcessing, job.JobId);
        }

        _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Chunking, job.JobId);
        var chunks = _chunkingStrategy.Chunk(command, extracted);
        _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
        await _ingestionChunkEnricher.EnrichAsync(chunks, job.ForceEmbeddingModel, false, ct);

        _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
            await _indexGateway.IndexDocumentChunksAsync(chunks, token);
        }, ct);

        _ingestionDocumentStateService.CompleteFullReindex(document, job.JobId, chunks.Count, ComputeHash(payload), command.ContentType, command.FileName);
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
}
