namespace Chatbot.Infrastructure.Configuration;

public sealed class ExternalProviderClientOptions
{
    public int TimeoutSeconds { get; set; }
    public string OpenAiCompatibleBaseUrl { get; set; } = string.Empty;
    public string OpenAiCompatibleApiKey { get; set; } = string.Empty;
    public string OpenAiCompatibleChatModel { get; set; } = string.Empty;
    public string OpenAiCompatibleVisionModel { get; set; } = string.Empty;

    public bool HasOpenAiCompatibleChatConfiguration(string? fallbackModel = null)
    {
        return HasConfiguredValue(OpenAiCompatibleBaseUrl)
            && HasConfiguredValue(ResolveOpenAiCompatibleChatModel(fallbackModel));
    }

    public bool HasOpenAiCompatibleVisionConfiguration()
    {
        return HasConfiguredValue(OpenAiCompatibleBaseUrl)
            && HasConfiguredValue(OpenAiCompatibleVisionModel);
    }

    public string ResolveOpenAiCompatibleChatModel(string? fallbackModel = null)
    {
        return HasConfiguredValue(OpenAiCompatibleChatModel)
            ? OpenAiCompatibleChatModel
            : fallbackModel ?? string.Empty;
    }

    public static bool HasConfiguredValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.Contains("[A PREENCHER]", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("example", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("dummy", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("changeme", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
    }
}
