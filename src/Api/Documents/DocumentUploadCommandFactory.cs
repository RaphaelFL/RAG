using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Chatbot.Api.Documents;

public class DocumentUploadCommandFactory : IDocumentUploadCommandFactory
{
	private readonly ILogger<DocumentUploadCommandFactory> _logger;

	public DocumentUploadCommandFactory(ILogger<DocumentUploadCommandFactory> logger)
	{
		_logger = logger;
	}

	public IngestDocumentCommand CreateSuggestionCommand(Guid documentId, Guid tenantId, IFormFile file, Stream content, DocumentUploadFormData? formData = null)
	{
		var command = new IngestDocumentCommand
		{
			DocumentId = documentId,
			TenantId = tenantId,
			FileName = file.FileName,
			ContentType = file.ContentType,
			ContentLength = file.Length,
			Content = content
		};

		ApplyClientExtraction(command, formData);
		return command;
	}

	public IngestDocumentCommand CreateUploadCommand(Guid documentId, Guid tenantId, IFormFile file, Stream content, DocumentUploadFormData formData)
	{
		var command = new IngestDocumentCommand
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

		ApplyClientExtraction(command, formData);
		return command;
	}

	private void ApplyClientExtraction(IngestDocumentCommand command, DocumentUploadFormData? formData)
	{
		if (formData is null)
		{
			return;
		}

		command.ClientExtractedText = string.IsNullOrWhiteSpace(formData.ExtractedText)
			? null
			: formData.ExtractedText.Trim();
		command.ClientExtractedPages = ParsePages(formData.ExtractedPagesJson);
	}

	private static List<string> ParseCsv(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new List<string>()
			: value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
	}

	private List<PageExtractionDto> ParsePages(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new List<PageExtractionDto>();
		}

		try
		{
			var pages = JsonSerializer.Deserialize<List<ClientExtractedPagePayload>>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return pages?
				.Where(page => page is not null && page.PageNumber > 0 && !string.IsNullOrWhiteSpace(page.Text))
				.Select(page => new PageExtractionDto
				{
					PageNumber = page!.PageNumber,
					Text = page.Text.Trim()
				})
				.ToList() ?? new List<PageExtractionDto>();
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Falha ao desserializar paginas extraidas no cliente.");
			return new List<PageExtractionDto>();
		}
	}

	private sealed class ClientExtractedPagePayload
	{
		public int PageNumber { get; set; }
		public string Text { get; set; } = string.Empty;
	}
}