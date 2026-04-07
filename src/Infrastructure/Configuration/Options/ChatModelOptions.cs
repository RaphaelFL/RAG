namespace Chatbot.Infrastructure.Configuration;

public sealed class ChatModelOptions
{
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public int MaxPromptContextTokens { get; set; } = 2800;
    public double TopP { get; set; }
}
