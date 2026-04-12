using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Chatbot.Api.Documents;

public interface IDocumentUploadCommandFactory
{
	IngestDocumentCommand CreateSuggestionCommand(Guid documentId, Guid tenantId, IFormFile file, Stream content, DocumentUploadFormData? formData = null);
	IngestDocumentCommand CreateUploadCommand(Guid documentId, Guid tenantId, IFormFile file, Stream content, DocumentUploadFormData formData);
}