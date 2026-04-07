namespace Chatbot.Application.Abstractions;

public interface IDocumentReindexService
{
    Task<ReindexDocumentResponseDto> ReindexAsync(Guid documentId, bool fullReindex, CancellationToken ct);
    Task<BulkReindexResponseDto> ReindexAsync(BulkReindexRequestDto request, Guid tenantId, CancellationToken ct);
}