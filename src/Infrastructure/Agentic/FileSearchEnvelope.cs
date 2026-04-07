namespace Chatbot.Infrastructure.Agentic;

internal sealed class FileSearchEnvelope
{
    public List<FileSearchMatch> Matches { get; set; } = new();
}