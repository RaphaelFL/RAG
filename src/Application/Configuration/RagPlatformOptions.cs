namespace Chatbot.Application.Configuration;

public sealed class EmbeddingGenerationOptions
{
    public string PrimaryRuntime { get; set; } = "python-local";
    public string ModelName { get; set; } = "intfloat/multilingual-e5-base";
    public string ModelVersion { get; set; } = "1";
    public string ModelPath { get; set; } = string.Empty;
    public string RuntimeCommand { get; set; } = "python";
    public string RuntimeArguments { get; set; } = string.Empty;
    public string RuntimeScriptPath { get; set; } = "tools/embeddings/embed_runtime.py";
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool NormalizeVectors { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 16;
    public int Dimensions { get; set; } = 1024;
    public int MaxConcurrency { get; set; } = 2;
}

public sealed class VectorStoreOptions
{
    public string Provider { get; set; } = "local-persistent";
    public string ConnectionString { get; set; } = string.Empty;
    public string Schema { get; set; } = "rag";
    public int Dimensions { get; set; } = 768;
    public string IndexName { get; set; } = "idx:rag:chunks";
    public string KeyPrefix { get; set; } = "rag:chunk:";
    public int CandidateMultiplier { get; set; } = 3;
    public int DefaultTopK { get; set; } = 8;
    public double DefaultScoreThreshold { get; set; } = 0.15;
}

public sealed class RedisCoordinationOptions
{
    public bool Enabled { get; set; } = true;
    public string Configuration { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "chatbot";
    public int LockTimeoutSeconds { get; set; } = 60;
}

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