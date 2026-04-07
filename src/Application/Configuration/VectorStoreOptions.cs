namespace Chatbot.Application.Configuration;

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