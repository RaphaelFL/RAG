using Chatbot.Application.Contracts;

namespace Chatbot.Application.Services;

internal sealed class PreparedChatTurnFactory
{
    public PreparedChatTurn Create(
        PromptTemplateDefinition template,
        DateTime startTime,
        ChatRetrievalContext retrievalContext,
        ChatCompletionResponse completionResponse)
    {
        var elapsed = DateTime.UtcNow - startTime;
        return new PreparedChatTurn
        {
            ResponseMessage = completionResponse.Message,
            Citations = retrievalContext.Citations,
            Usage = BuildUsage(completionResponse.CompletionResult, elapsed, retrievalContext),
            Policy = new ChatPolicyDto
            {
                Grounded = retrievalContext.EvidenceChunks.Count > 0,
                HadEnoughEvidence = retrievalContext.EvidenceChunks.Count > 0,
                TemplateId = template.TemplateId,
                TemplateVersion = template.Version
            }
        };
    }

    private static UsageMetadataDto BuildUsage(
        ChatCompletionResult? completionResult,
        TimeSpan elapsed,
        ChatRetrievalContext retrievalContext)
    {
        return new UsageMetadataDto
        {
            Model = completionResult?.Model ?? "policy-only",
            PromptTokens = completionResult?.PromptTokens ?? 0,
            CompletionTokens = completionResult?.CompletionTokens ?? 0,
            TotalTokens = completionResult?.TotalTokens ?? 0,
            LatencyMs = (long)elapsed.TotalMilliseconds,
            RetrievalStrategy = ResolveRetrievalStrategy(
                retrievalContext.AttemptedRetrieval,
                retrievalContext.RetrievalResult.RetrievalStrategy,
                retrievalContext.EvidenceChunks.Count,
                retrievalContext.AllowsGeneralKnowledge),
            RuntimeMetrics = new Dictionary<string, long>
            {
                ["max_context_chunks"] = retrievalContext.MaxContextChunks,
                ["retrieved_chunks"] = retrievalContext.RetrievalResult.Chunks.Count,
                ["evidence_chunks"] = retrievalContext.EvidenceChunks.Count,
                ["citations"] = retrievalContext.Citations.Count
            }
        };
    }

    private static string ResolveRetrievalStrategy(bool attemptedRetrieval, string retrievalStrategy, int evidenceChunkCount, bool allowsGeneralKnowledge)
    {
        if (evidenceChunkCount > 0)
        {
            return allowsGeneralKnowledge ? $"auto-hybrid:{retrievalStrategy}" : $"auto-rag:{retrievalStrategy}";
        }

        if (attemptedRetrieval)
        {
            return allowsGeneralKnowledge ? "auto-llm:fallback-after-retrieval" : retrievalStrategy;
        }

        return allowsGeneralKnowledge ? "auto-llm" : "grounded";
    }
}