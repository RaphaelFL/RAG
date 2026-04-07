namespace Chatbot.Application.Configuration;

public sealed class AgentRuntimeOptions
{
    public bool Enabled { get; set; }
    public int MaxToolBudget { get; set; } = 8;
    public int MaxDepth { get; set; } = 4;
    public int DefaultTimeoutSeconds { get; set; } = 30;
}

public sealed class CodeInterpreterOptions
{
    public bool Enabled { get; set; }
    public string Runtime { get; set; } = "python";
    public int TimeoutSeconds { get; set; } = 20;
    public int MemoryLimitMb { get; set; } = 512;
    public string WorkingDirectoryRoot { get; set; } = "artifacts/code-interpreter";
}

public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }
    public int DefaultTopK { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 10;
    public string[] AllowedHosts { get; set; } = Array.Empty<string>();
    public string[] DeniedHosts { get; set; } = Array.Empty<string>();
}

public sealed class StructuredExtractionOptions
{
    public bool EnableForms { get; set; } = true;
    public bool EnableTables { get; set; } = true;
    public bool EnableSpreadsheetStructure { get; set; } = true;
    public bool EnablePresentationStructure { get; set; } = true;
}