using System.Security.Cryptography;
using System.Text;

namespace Chatbot.Application.Services;

public sealed class ChatCompletionCacheKeyFactory : IChatCompletionCacheKeyFactory
{
    public string Build(ChatRequestDto request, PromptTemplateDefinition template, AgenticChatPlan plan, IReadOnlyCollection<RetrievedChunkDto> chunks)
    {
        var fingerprint = string.Join('|', chunks
            .Select(chunk => $"{chunk.ChunkId}:{chunk.DocumentTitle}:{chunk.PageNumber}:{chunk.EndPageNumber}:{chunk.Section}:{ComputeHash(chunk.Content)}:{chunk.Score.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}")
            .OrderBy(value => value, StringComparer.Ordinal));

        return $"chat-completion:{ComputeHash(string.Join("||", new[]
        {
            request.Message.Trim(),
            template.TemplateId,
            template.Version,
            plan.ExecutionMode,
            plan.AllowsGeneralKnowledge.ToString(),
            fingerprint
        }))}";
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}