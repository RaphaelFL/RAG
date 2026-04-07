namespace Chatbot.Application.Abstractions;

public sealed class ReindexBackgroundJob
{
    public Guid JobId { get; set; }
    public Guid DocumentId { get; set; }
    public bool FullReindex { get; set; }
    public string? ForceEmbeddingModel { get; set; }
}
