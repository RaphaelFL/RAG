using System.Security.Cryptography;
using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

public sealed class FullReindexProcessor : IFullReindexProcessor
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

    public FullReindexProcessor(
        IIngestionCommandFactory ingestionCommandFactory,
        IIngestionExtractionService ingestionExtractionService,
        IIngestionChunkEnricher ingestionChunkEnricher,
        IIngestionDocumentStateService ingestionDocumentStateService,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IBlobStorageGateway blobGateway,
        ResiliencePipeline resiliencePipeline)
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
    }

    public async Task ProcessAsync(ReindexBackgroundJob job, CancellationToken ct)
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