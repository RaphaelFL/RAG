namespace Chatbot.Application.Abstractions;

public interface IDocumentQueryService : IDocumentCatalogReader, IDocumentInspectionReader, IDocumentChunkEmbeddingReader
{
}