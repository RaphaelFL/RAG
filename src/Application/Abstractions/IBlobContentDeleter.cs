namespace Chatbot.Application.Abstractions;

public interface IBlobContentDeleter
{
    Task DeleteAsync(string path, CancellationToken ct);
}