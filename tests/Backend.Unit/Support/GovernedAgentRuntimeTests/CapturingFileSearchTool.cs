using Chatbot.Application.Abstractions;

namespace Backend.Unit.GovernedAgentRuntimeTestsSupport;

internal sealed class CapturingFileSearchTool : IFileSearchTool
{
    public FileSearchRequest? LastRequest { get; private set; }

    public Task<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(new FileSearchResult
        {
            Matches = new[]
            {
                new RetrievedChunk
                {
                    ChunkId = "chunk-1",
                    DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Score = 0.9,
                    Text = "Politica de viagens"
                }
            }
        });
    }
}