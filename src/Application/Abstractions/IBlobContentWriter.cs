namespace Chatbot.Application.Abstractions;

public interface IBlobContentWriter
{
    Task<string> SaveAsync(Stream content, string path, CancellationToken ct);
}