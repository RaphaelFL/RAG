namespace Chatbot.Application.Configuration;

public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }
    public int DefaultTopK { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 10;
    public string[] AllowedHosts { get; set; } = Array.Empty<string>();
    public string[] DeniedHosts { get; set; } = Array.Empty<string>();
}
