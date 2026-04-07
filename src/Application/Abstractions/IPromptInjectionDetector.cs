namespace Chatbot.Application.Abstractions;

public interface IPromptInjectionDetector
{
    bool TryDetect(string? input, out string pattern);
}