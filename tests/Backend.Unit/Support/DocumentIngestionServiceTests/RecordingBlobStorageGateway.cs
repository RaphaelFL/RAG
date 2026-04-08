using Chatbot.Application.Abstractions;

namespace Backend.Unit.DocumentIngestionServiceTestsSupport;

internal sealed class RecordingBlobStorageGateway : IBlobStorageGateway
{
    public List<string> SavedPaths { get; } = new();

    public Task DeleteAsync(string path, CancellationToken ct) => Task.CompletedTask;

    public Task<Stream> GetAsync(string path, CancellationToken ct)
    {
        return Task.FromResult<Stream>(Stream.Null);
    }

    public Task<string> SaveAsync(Stream content, string path, CancellationToken ct)
    {
        SavedPaths.Add(path);
        return Task.FromResult(path);
    }
}