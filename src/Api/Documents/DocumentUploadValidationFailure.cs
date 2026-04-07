namespace Chatbot.Api.Documents;

public class DocumentUploadValidationFailure
{
	public int StatusCode { get; init; }
	public string Code { get; init; } = string.Empty;
	public string Message { get; init; } = string.Empty;
}