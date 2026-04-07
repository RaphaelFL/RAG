using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Chatbot.Api.Documents;

public class DocumentUploadCommandFactory : IDocumentUploadCommandFactory
{
	public IngestDocumentCommand CreateSuggestionCommand(Guid documentId, Guid tenantId, IFormFile file, Stream content)
	{
		return new IngestDocumentCommand
		{
			DocumentId = documentId,
			TenantId = tenantId,
			FileName = file.FileName,
			ContentType = file.ContentType,
			ContentLength = file.Length,
			Content = content
		};
	}

	public IngestDocumentCommand CreateUploadCommand(Guid documentId, Guid tenantId, IFormFile file, Stream content, DocumentUploadFormData formData)
	{
		return new IngestDocumentCommand
		{
			DocumentId = documentId,
			TenantId = tenantId,
			FileName = file.FileName,
			ContentType = file.ContentType,
			ContentLength = file.Length,
			DocumentTitle = formData.DocumentTitle,
			Category = formData.Category,
			Tags = ParseCsv(formData.Tags),
			Categories = ParseCsv(formData.Categories),
			Source = formData.Source,
			ExternalId = formData.ExternalId,
			AccessPolicy = formData.AccessPolicy,
			Content = content
		};
	}

	private static List<string> ParseCsv(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new List<string>()
			: value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
	}
}