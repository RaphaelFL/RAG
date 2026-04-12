using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Chunking;

internal sealed class SlidingWindowWindowResolver
{
    private readonly IRagRuntimeSettings _runtimeSettings;

    public SlidingWindowWindowResolver(IRagRuntimeSettings runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
    }

    public int ResolveMaxTokens(string text, long contentLength, int pageCount)
    {
        var (chunkSize, _) = ResolveWindow(text, contentLength, pageCount);
        return Math.Max(32, EstimateTokens(chunkSize));
    }

    public int ResolveOverlapTokens(string text, long contentLength, int pageCount)
    {
        var (_, overlap) = ResolveWindow(text, contentLength, pageCount);
        return Math.Max(8, EstimateTokens(Math.Max(0, overlap)));
    }

    private (int ChunkSize, int Overlap) ResolveWindow(string text, long contentLength, int pageCount)
    {
        var isDense = IsDenseContent(text);
        var baseChunkSize = isDense ? _runtimeSettings.DenseChunkSize : _runtimeSettings.NarrativeChunkSize;
        var baseOverlap = isDense ? _runtimeSettings.DenseOverlap : _runtimeSettings.NarrativeOverlap;
        var scale = ResolveDocumentScale(text.Length, contentLength, pageCount, baseChunkSize);
        var chunkSize = Math.Max(_runtimeSettings.MinimumChunkCharacters, (int)Math.Round(baseChunkSize * scale, MidpointRounding.AwayFromZero));
        var overlap = Math.Max(0, (int)Math.Round(baseOverlap * scale, MidpointRounding.AwayFromZero));

        return (chunkSize, Math.Min(Math.Max(0, chunkSize - 1), overlap));
    }

    private static double ResolveDocumentScale(int textLength, long contentLength, int pageCount, int baseChunkSize)
    {
        if (pageCount >= 20 || contentLength >= 12_000_000 || textLength >= baseChunkSize * 18)
        {
            return 0.7d;
        }

        if (pageCount >= 10 || contentLength >= 5_000_000 || textLength >= baseChunkSize * 10)
        {
            return 0.8d;
        }

        if (pageCount >= 5 || contentLength >= 1_500_000 || textLength >= baseChunkSize * 5)
        {
            return 0.9d;
        }

        return 1d;
    }

    private static int EstimateTokens(int characterCount)
    {
        return Math.Max(1, (int)Math.Ceiling(characterCount / 4d));
    }

    private static bool IsDenseContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lineBreaks = text.Count(character => character == '\n');
        var digits = text.Count(char.IsDigit);
        var bulletMarkers = text.Count(character => character is '-' or '*' or '•');
        var lineBreakRatio = lineBreaks / (double)Math.Max(1, text.Length);
        var digitRatio = digits / (double)Math.Max(1, text.Length);

        return lineBreakRatio > 0.02 || digitRatio > 0.08 || bulletMarkers >= 8;
    }
}