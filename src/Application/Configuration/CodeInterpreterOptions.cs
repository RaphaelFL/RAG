namespace Chatbot.Application.Configuration;

public sealed class CodeInterpreterOptions
{
    public bool Enabled { get; set; }
    public string Runtime { get; set; } = "python";
    public int TimeoutSeconds { get; set; } = 20;
    public int MemoryLimitMb { get; set; } = 512;
    public string WorkingDirectoryRoot { get; set; } = "artifacts/code-interpreter";
}
