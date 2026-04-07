namespace Chatbot.Application.Abstractions;

public interface IChunkingStrategy
{
    List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction);
}