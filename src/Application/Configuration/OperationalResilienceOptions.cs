namespace Chatbot.Application.Configuration;

public sealed class OperationalResilienceOptions
{
    public int TimeoutSeconds { get; set; } = 180;
}