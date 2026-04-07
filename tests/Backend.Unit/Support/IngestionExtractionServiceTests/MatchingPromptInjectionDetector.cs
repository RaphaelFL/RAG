using Chatbot.Application.Abstractions;

namespace Backend.Unit.IngestionExtractionServiceTestsSupport;

internal sealed class MatchingPromptInjectionDetector : IPromptInjectionDetector
{
    private readonly string _pattern;

    public MatchingPromptInjectionDetector(string pattern)
    {
        _pattern = pattern;
    }

    public bool TryDetect(string? input, out string pattern)
    {
        pattern = _pattern;
        return true;
    }
}