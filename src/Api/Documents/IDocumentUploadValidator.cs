using Microsoft.AspNetCore.Http;

namespace Chatbot.Api.Documents;

public interface IDocumentUploadValidator
{
	DocumentUploadValidationFailure? Validate(IFormFile file);
}