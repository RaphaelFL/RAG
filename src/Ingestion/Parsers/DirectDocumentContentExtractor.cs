using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DirectDocumentContentExtractor
{
    private readonly DirectDocumentClassifier _classifier;
    private readonly DocxArchiveTextExtractor _docxArchiveTextExtractor;
    private readonly DirectTextPayloadExtractor _directTextPayloadExtractor;

    public DirectDocumentContentExtractor(
        DirectDocumentClassifier classifier,
        DocxArchiveTextExtractor docxArchiveTextExtractor,
        DirectTextPayloadExtractor directTextPayloadExtractor)
    {
        _classifier = classifier;
        _docxArchiveTextExtractor = docxArchiveTextExtractor;
        _directTextPayloadExtractor = directTextPayloadExtractor;
    }

    public string? Extract(IngestDocumentCommand command, byte[] bytes)
    {
        var extension = Path.GetExtension(command.FileName);

        if (_classifier.IsPdf(extension))
        {
            return PdfTextExtraction.TryExtractText(bytes);
        }

        if (_classifier.IsDocx(extension))
        {
            return _docxArchiveTextExtractor.TryExtract(bytes);
        }

        return _classifier.IsTextPayload(extension, command.ContentType, bytes)
            ? _directTextPayloadExtractor.Extract(bytes)
            : null;
    }
}