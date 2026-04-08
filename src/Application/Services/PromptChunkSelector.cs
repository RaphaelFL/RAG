namespace Chatbot.Application.Services;

public sealed class PromptChunkSelector : IPromptChunkSelector
{
    public IReadOnlyList<RetrievedChunk> Select(PromptAssemblyRequest request)
    {
        return request.Chunks
            .GroupBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(chunk => chunk.Score)
            .ToArray();
    }
}