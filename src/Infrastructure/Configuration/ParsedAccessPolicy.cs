namespace Chatbot.Infrastructure.Configuration;

internal sealed class ParsedAccessPolicy
{
    public static ParsedAccessPolicy Empty { get; } = new();

    public bool AllowPlatformAdminCrossTenant { get; init; }

    public List<string> AllowedRoles { get; init; } = new();

    public List<string> AllowedUserIds { get; init; } = new();
}