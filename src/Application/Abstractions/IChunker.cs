namespace Chatbot.Application.Abstractions;

public interface IChunker
{
    string StrategyName { get; }
    Task<IReadOnlyCollection<ChunkCandidate>> ChunkAsync(ChunkingRequest request, CancellationToken ct);
}