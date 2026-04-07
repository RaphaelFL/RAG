using Chatbot.Application.Abstractions;

namespace Backend.Unit.IngestionExtractionServiceTestsSupport;

internal sealed class NonMatchingPromptInjectionDetector : IPromptInjectionDetector
{
    public bool TryDetect(string? input, out string pattern)
    {
        pattern = string.Empty;
        return false;
    }
}