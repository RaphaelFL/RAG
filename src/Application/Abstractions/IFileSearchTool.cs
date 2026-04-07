namespace Chatbot.Application.Abstractions;

public interface IFileSearchTool
{
    Task<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken ct);
}