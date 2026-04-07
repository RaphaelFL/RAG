using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Chunking;

internal sealed class SlidingWindowWindowResolver
{
    private readonly IRagRuntimeSettings _runtimeSettings;

    public SlidingWindowWindowResolver(IRagRuntimeSettings runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
    }

    public int ResolveMaxTokens(string text)
    {
        var (chunkSize, _) = ResolveWindow(text);
        return Math.Max(32, EstimateTokens(chunkSize));
    }

    public int ResolveOverlapTokens(string text)
    {
        var (_, overlap) = ResolveWindow(text);
        return Math.Max(8, EstimateTokens(Math.Max(0, overlap)));
    }

    private (int ChunkSize, int Overlap) ResolveWindow(string text)
    {
        return IsDenseContent(text)
            ? (Math.Max(_runtimeSettings.MinimumChunkCharacters, _runtimeSettings.DenseChunkSize), Math.Max(0, _runtimeSettings.DenseOverlap))
            : (Math.Max(_runtimeSettings.MinimumChunkCharacters, _runtimeSettings.NarrativeChunkSize), Math.Max(0, _runtimeSettings.NarrativeOverlap));
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