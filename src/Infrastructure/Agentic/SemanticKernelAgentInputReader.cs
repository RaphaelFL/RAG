namespace Chatbot.Infrastructure.Agentic;

internal sealed class SemanticKernelAgentInputReader
{
    public string ReadString(Dictionary<string, object?> input, string key, string fallback = "")
    {
        return input.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;
    }

    public int ReadInt(Dictionary<string, object?> input, string key, int fallback)
    {
        return input.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;
    }
}