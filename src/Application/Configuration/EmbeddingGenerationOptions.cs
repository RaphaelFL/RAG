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