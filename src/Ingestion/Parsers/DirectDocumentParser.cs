using System.IO.Compression;
using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DirectDocumentParser : IDocumentParser
{
    private readonly DirectDocumentClassifier _classifier = new();
    private readonly DirectDocumentContentExtractor _contentExtractor;

    public DirectDocumentParser()
    {
        _contentExtractor = new DirectDocumentContentExtractor(
            _classifier,
            new DocxArchiveTextExtractor(),
            new DirectTextPayloadExtractor());
    }

    public bool CanParse(IngestDocumentCommand command)
    {
        return _classifier.CanParse(command);
    }

    public async Task<DocumentParseResultDto?> ParseAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        if (!CanParse(command))
        {
            return null;
        }

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        using var ms = new MemoryStream();
        await command.Content.CopyToAsync(ms, ct);

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        var bytes = ms.ToArray();

        var text = _contentExtractor.Extract(command, bytes);
        return string.IsNullOrWhiteSpace(text)
            ? null
            : new DocumentParseResultDto
            {
                Text = text
            };
    }
}