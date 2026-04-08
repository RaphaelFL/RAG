namespace Chatbot.Application.Abstractions;

public interface IBlobContentReader
{
    Task<Stream> GetAsync(string path, CancellationToken ct);
}