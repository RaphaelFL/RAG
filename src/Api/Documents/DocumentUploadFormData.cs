namespace Chatbot.Api.Documents;

public class DocumentUploadFormData
{
	public string? DocumentTitle { get; set; }
	public string? Category { get; set; }
	public string? Tags { get; set; }
	public string? Categories { get; set; }
	public string? Source { get; set; }
	public string? ExternalId { get; set; }
	public string? AccessPolicy { get; set; }
	public string? ExtractedText { get; set; }
	public string? ExtractedPagesJson { get; set; }
}