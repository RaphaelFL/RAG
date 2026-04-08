using Chatbot.Application.Contracts;

namespace Chatbot.Application.Abstractions;

public sealed class PreparedChatTurn
{
    public string ResponseMessage { get; init; } = string.Empty;
    public List<CitationDto> Citations { get; init; } = new();
    public UsageMetadataDto Usage { get; init; } = new();
    public ChatPolicyDto Policy { get; init; } = new();
}