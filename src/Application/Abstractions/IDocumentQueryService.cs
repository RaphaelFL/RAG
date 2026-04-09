namespace Chatbot.Application.Abstractions;

public interface IDocumentQueryService : IDocumentCatalogReader, IDocumentInspectionReader, IDocumentChunkEmbeddingReader, IOriginalDocumentReader
{
	Task<DocumentTextPreviewDto?> GetDocumentTextPreviewAsync(Guid documentId, CancellationToken ct);
}