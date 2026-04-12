namespace Chatbot.Application.Services;

public sealed class IngestionCommandFactory : IIngestionCommandFactory
{
    public IngestDocumentCommand Create(IngestionBackgroundJob job)
    {
        return new IngestDocumentCommand
        {
            DocumentId = job.DocumentId,
            TenantId = job.TenantId,
            FileName = job.FileName,
            ContentType = job.ContentType,
            ContentLength = job.ContentLength,
            DocumentTitle = job.DocumentTitle,
            Category = job.Category,
            Tags = new List<string>(job.Tags),
            Categories = new List<string>(job.Categories),
            Source = job.Source,
            ExternalId = job.ExternalId,
            AccessPolicy = job.AccessPolicy,
            ClientExtractedText = job.ClientExtractedText,
            ClientExtractedPages = job.ClientExtractedPages.Select(page => new PageExtractionDto
            {
                PageNumber = page.PageNumber,
                Text = page.Text,
                WorksheetName = page.WorksheetName,
                SlideNumber = page.SlideNumber,
                SectionTitle = page.SectionTitle,
                TableId = page.TableId,
                FormId = page.FormId,
                Metadata = new Dictionary<string, string>(page.Metadata, StringComparer.OrdinalIgnoreCase),
                Tables = page.Tables
            }).ToList(),
            Content = new MemoryStream(job.Payload, writable: false)
        };
    }

    public IngestDocumentCommand Create(DocumentCatalogEntry document, byte[] payload)
    {
        var fileName = string.IsNullOrWhiteSpace(document.OriginalFileName)
            ? BuildFallbackFileName(document)
            : document.OriginalFileName;

        return new IngestDocumentCommand
        {
            DocumentId = document.DocumentId,
            TenantId = document.TenantId,
            FileName = fileName,
            ContentType = document.ContentType,
            ContentLength = payload.LongLength,
            DocumentTitle = document.Title,
            Category = document.Category,
            Tags = new List<string>(document.Tags),
            Categories = new List<string>(document.Categories),
            Source = document.Source,
            ExternalId = document.ExternalId,
            AccessPolicy = document.AccessPolicy,
            ClientExtractedText = document.ClientExtractedText,
            ClientExtractedPages = document.ClientExtractedPages.Select(page => new PageExtractionDto
            {
                PageNumber = page.PageNumber,
                Text = page.Text,
                WorksheetName = page.WorksheetName,
                SlideNumber = page.SlideNumber,
                SectionTitle = page.SectionTitle,
                TableId = page.TableId,
                FormId = page.FormId,
                Metadata = new Dictionary<string, string>(page.Metadata, StringComparer.OrdinalIgnoreCase),
                Tables = page.Tables
            }).ToList(),
            Content = new MemoryStream(payload, writable: false)
        };
    }

    private static string BuildFallbackFileName(DocumentCatalogEntry document)
    {
        var extension = document.ContentType switch
        {
            "application/pdf" => ".pdf",
            "text/plain" => ".txt",
            "text/markdown" => ".md",
            "text/html" => ".html",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            _ => ".bin"
        };

        return $"{document.Title}{extension}";
    }
}