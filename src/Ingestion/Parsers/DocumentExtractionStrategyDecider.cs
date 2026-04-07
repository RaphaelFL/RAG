using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Ingestion.Parsers;

public sealed class DocumentExtractionStrategyDecider : IDocumentExtractionStrategyDecider
{
    private readonly OcrOptions _ocrOptions;

    public DocumentExtractionStrategyDecider(IOptions<OcrOptions> ocrOptions)
    {
        _ocrOptions = ocrOptions.Value;
    }

    public bool ShouldUseDirectText(IngestDocumentCommand command, string? parsedText)
    {
        if (string.IsNullOrWhiteSpace(parsedText))
        {
            return false;
        }

        if (IsPdfDocument(command) && PdfTextExtraction.LooksLikeArtifactText(parsedText))
        {
            return false;
        }

        if (!_ocrOptions.EnableSelectiveOcr || IsTextFirstDocument(command))
        {
            return true;
        }

        if (parsedText.Length >= _ocrOptions.MinimumDirectTextCharacters)
        {
            return true;
        }

        if (command.ContentLength <= 0)
        {
            return false;
        }

        var coverageRatio = parsedText.Length / (double)Math.Max(1, command.ContentLength);
        return coverageRatio >= _ocrOptions.MinimumDirectTextCoverageRatio;
    }

    public bool ShouldAttemptOcr(IngestDocumentCommand command, string? parsedText)
    {
        if (!_ocrOptions.EnableSelectiveOcr)
        {
            return true;
        }

        if (IsImageDocument(command))
        {
            return true;
        }

        if (IsTextFirstDocument(command))
        {
            return false;
        }

        if (IsPdfDocument(command))
        {
            return !ShouldUseDirectText(command, parsedText);
        }

        return false;
    }

    public bool ShouldRecordOcrAvoided(IngestDocumentCommand command)
    {
        return IsPdfDocument(command) || IsImageDocument(command);
    }

    private static bool IsTextFirstDocument(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        return extension is ".txt" or ".md" or ".html" or ".htm";
    }

    private static bool IsPdfDocument(IngestDocumentCommand command)
    {
        return string.Equals(Path.GetExtension(command.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageDocument(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        return command.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || extension is ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp";
    }
}