namespace Chatbot.Application.Abstractions;

public interface IIngestionCommandFactory
{
    IngestDocumentCommand Create(IngestionBackgroundJob job);
    IngestDocumentCommand Create(DocumentCatalogEntry document, byte[] payload);
}