namespace Chatbot.Application.Abstractions;

public sealed class RetrievalExecutionPlan
{
    public required FileSearchFilterDto Filters { get; init; }

    public required int RequestedTopK { get; init; }

    public required int CandidateCount { get; init; }

    public required bool SemanticRankingEnabled { get; init; }

    public required string CacheKey { get; init; }
}