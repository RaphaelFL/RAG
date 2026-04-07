using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisSearchResultAccumulator
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double VectorScore { get; set; }
    public double LexicalScore { get; set; }
    public double Score { get; set; }

    public SearchResultDto ToDto()
    {
        return new SearchResultDto
        {
            ChunkId = ChunkId,
            DocumentId = DocumentId,
            Content = Content,
            Score = Score,
            Metadata = Metadata
        };
    }
}