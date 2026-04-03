using Chatbot.Application.Contracts;
using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;

namespace Chatbot.Api.Controllers;

/// <summary>
/// Endpoint para ingestão e gerenciamento de documentos
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/documents")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".png", ".jpg", ".jpeg"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".html", ".htm", ".csv", ".tsv", ".json", ".xml", ".yaml", ".yml", ".log", ".ini", ".cfg", ".sql"
    };

    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".msi", ".bat", ".cmd", ".com", ".scr", ".ps1", ".jar", ".js", ".vbs", ".wsf", ".sh"
    };

    private static readonly HashSet<string> BinaryContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/png",
        "image/jpeg"
    };

    private static readonly HashSet<string> TextApplicationContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/xml",
        "application/yaml",
        "application/x-yaml",
        "application/sql",
        "text/xml"
    };

    private readonly IIngestionPipeline _ingestionPipeline;
    private readonly IDocumentMetadataSuggestionService _documentMetadataSuggestionService;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IIngestionPipeline ingestionPipeline,
        IDocumentMetadataSuggestionService documentMetadataSuggestionService,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<DocumentsController> logger)
    {
        _ingestionPipeline = ingestionPipeline;
        _documentMetadataSuggestionService = documentMetadataSuggestionService;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Sugere titulo logico, categoria e tags antes da ingestao definitiva.
    /// </summary>
    [HttpPost("suggest-metadata")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [ProducesResponseType(typeof(DocumentMetadataSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentMetadataSuggestionDto>> SuggestMetadata(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document metadata suggestion initiated: {filename}", file.FileName);

        var validationError = ValidateIncomingFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var suggestion = await _documentMetadataSuggestionService.SuggestAsync(new IngestDocumentCommand
            {
                DocumentId = Guid.NewGuid(),
                TenantId = GetRequiredTenantId(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                ContentLength = file.Length,
                Content = stream
            }, cancellationToken);

            return Ok(suggestion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting metadata for document {filename}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto
            {
                Code = "metadata_suggestion_failed",
                Message = "Failed to analyze document metadata",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Upload de novo documento
    /// </summary>
    [HttpPost("ingest")]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadDocumentResponseDto>> Upload(
        IFormFile file,
        [FromForm] string? documentTitle,
        [FromForm] string? category,
        [FromForm] string? tags,
        [FromForm] string? categories,
        [FromForm] string? source,
        [FromForm] string? externalId,
        [FromForm] string? accessPolicy,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document upload initiated: {filename}", file.FileName);

        var validationError = ValidateIncomingFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        try
        {
            var documentId = Guid.NewGuid();
            var tenantId = GetRequiredTenantId();

            using (var stream = file.OpenReadStream())
            {
                var response = await _ingestionPipeline.IngestAsync(new IngestDocumentCommand
                {
                    DocumentId = documentId,
                    TenantId = tenantId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    ContentLength = file.Length,
                    DocumentTitle = documentTitle,
                    Category = category,
                    Tags = ParseCsv(tags),
                    Categories = ParseCsv(categories),
                    Source = source,
                    ExternalId = externalId,
                    AccessPolicy = accessPolicy,
                    Content = stream
                }, cancellationToken);
                return Accepted(response);
            }
        }
        catch (DuplicateDocumentException ex)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, ex.Message);
            return Conflict(new ErrorResponseDto
            {
                Code = "document_conflict",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
        catch (InvalidOperationException ex)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, ex.Message);
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_file",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto
            {
                Code = "upload_failed",
                Message = "Failed to upload document",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Reindexar um documento específico.
    /// </summary>
    [HttpPost("{documentId}/reindex")]
    [Authorize(Policy = "DocumentAdmin")]
    [EnableRateLimiting("reindex")]
    [ProducesResponseType(typeof(ReindexDocumentResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReindexDocumentResponseDto>> Reindex(
        [FromRoute] Guid documentId,
        [FromBody] ReindexDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reindex initiated for document {documentId}, full: {isFull}", 
            documentId, request.FullReindex);

        try
        {
            var response = await _ingestionPipeline.ReindexAsync(documentId, request.FullReindex, cancellationToken);
            return Accepted(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponseDto
            {
                Code = "document_not_found",
                Message = $"Document {documentId} not found",
                TraceId = HttpContext.TraceIdentifier
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto
            {
                Code = "access_denied",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Reindexar um conjunto de documentos existentes.
    /// </summary>
    [HttpPost("reindex")]
    [Authorize(Policy = "DocumentAdmin")]
    [EnableRateLimiting("reindex")]
    [ProducesResponseType(typeof(BulkReindexResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BulkReindexResponseDto>> ReindexMany(
        [FromBody] BulkReindexRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _ingestionPipeline.ReindexAsync(request, GetRequiredTenantId(), cancellationToken);
            return Accepted(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto
            {
                Code = "access_denied",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Consultar o estado operacional e metadados de um documento.
    /// </summary>
    [HttpGet("{documentId}")]
    [ProducesResponseType(typeof(DocumentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsDto>> GetDocument(
        [FromRoute] Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await _ingestionPipeline.GetDocumentAsync(documentId, cancellationToken);
            if (document is null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Code = "document_not_found",
                    Message = $"Document {documentId} not found",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            return Ok(document);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto
            {
                Code = "access_denied",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    private Guid GetRequiredTenantId()
    {
        var rawTenantId = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(rawTenantId, out var tenantId))
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return tenantId;
    }

    private static List<string> ParseCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void ValidateUpload(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (HasDangerousExtension(extension))
        {
            throw new InvalidOperationException("File extension is not supported.");
        }

        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[512];
        var bytesRead = stream.Read(header);

        if (!IsSupportedUpload(extension, file.ContentType, header[..bytesRead]))
        {
            throw new InvalidOperationException("File type is not supported.");
        }
    }

    private static bool IsSupportedUpload(string extension, string? contentType, ReadOnlySpan<byte> header)
    {
        if (header.Length == 0)
        {
            return false;
        }

        if (IsBinaryDocument(extension, contentType))
        {
            return HasValidSignature(extension, contentType, header);
        }

        return IsKnownTextExtension(extension)
            || IsTextContentType(contentType)
            || IsTextLike(header);
    }

    private static bool HasValidSignature(string extension, string? contentType, ReadOnlySpan<byte> header)
    {
        if (header.Length == 0)
        {
            return false;
        }

        if (MatchesBinaryType(extension, contentType, ".pdf", "application/pdf"))
        {
            return header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;
        }

        if (MatchesBinaryType(extension, contentType, ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"))
        {
            return header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B;
        }

        if (MatchesBinaryType(extension, contentType, ".png", "image/png"))
        {
            return header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        }

        if (MatchesBinaryType(extension, contentType, ".jpg", "image/jpeg") || MatchesBinaryType(extension, contentType, ".jpeg", "image/jpeg"))
        {
            return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        return false;
    }

    private static bool MatchesBinaryType(string extension, string? contentType, string expectedExtension, string expectedContentType)
    {
        return string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, expectedContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBinaryDocument(string extension, string? contentType)
    {
        return BinaryExtensions.Contains(extension)
            || (!string.IsNullOrWhiteSpace(contentType) && BinaryContentTypes.Contains(contentType));
    }

    private static bool IsKnownTextExtension(string extension)
    {
        return TextExtensions.Contains(extension);
    }

    private static bool IsTextContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || TextApplicationContentTypes.Contains(contentType);
    }

    private static bool HasDangerousExtension(string extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && DangerousExtensions.Contains(extension);
    }

    private static bool IsTextLike(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            return true;
        }

        if (header.Length >= 2)
        {
            var hasUtf16Bom = (header[0] == 0xFF && header[1] == 0xFE) || (header[0] == 0xFE && header[1] == 0xFF);
            if (hasUtf16Bom)
            {
                return true;
            }
        }

        var printable = 0;
        var alphaNumeric = 0;

        foreach (var value in header)
        {
            if (value == 0)
            {
                return false;
            }

            if (value is 9 or 10 or 13 || value is >= 32 and <= 126 || value >= 160)
            {
                printable++;
            }

            if ((value is >= (byte)'0' and <= (byte)'9')
                || (value is >= (byte)'A' and <= (byte)'Z')
                || (value is >= (byte)'a' and <= (byte)'z'))
            {
                alphaNumeric++;
            }
        }

        var preview = Encoding.UTF8.GetString(header);
        if (string.IsNullOrWhiteSpace(preview))
        {
            return false;
        }

        var printableRatio = printable / (double)header.Length;
        return printableRatio >= 0.85 && (alphaNumeric > 0 || printable == header.Length);
    }

    private ActionResult? ValidateIncomingFile(IFormFile file)
    {
        if (file.Length == 0)
        {
            return BadRequest(new ErrorResponseDto
            {
                Code = "empty_file",
                Message = "File is empty",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (file.Length > 100 * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ErrorResponseDto
            {
                Code = "file_too_large",
                Message = "File exceeds maximum size of 100MB",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        try
        {
            ValidateUpload(file);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, ex.Message);
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_file",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }
}
