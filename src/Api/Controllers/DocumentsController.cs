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
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md", ".html", ".htm", ".png", ".jpg", ".jpeg"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown",
        "text/html",
        "image/png",
        "image/jpeg"
    };

    private readonly IIngestionPipeline _ingestionPipeline;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IIngestionPipeline ingestionPipeline, ISecurityAuditLogger securityAuditLogger, ILogger<DocumentsController> logger)
    {
        _ingestionPipeline = ingestionPipeline;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
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

        try
        {
            if (file.Length == 0)
                return BadRequest(new ErrorResponseDto
                {
                    Code = "empty_file",
                    Message = "File is empty",
                    TraceId = HttpContext.TraceIdentifier
                });

            if (file.Length > 100 * 1024 * 1024) // 100 MB
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new ErrorResponseDto
                {
                    Code = "file_too_large",
                    Message = "File exceeds maximum size of 100MB",
                    TraceId = HttpContext.TraceIdentifier
                });

            ValidateUpload(file);

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
            var response = await _ingestionPipeline.ReindexAsync(request, cancellationToken);
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
        var rawTenantId = User.FindFirst("tenant_id")?.Value ?? HttpContext.Request.Headers["X-Tenant-Id"].ToString();
        return Guid.Parse(rawTenantId);
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
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("File extension is not supported.");
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("File content type is not supported.");
        }

        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[8];
        var bytesRead = stream.Read(header);

        if (!HasValidSignature(extension, header[..bytesRead]))
        {
            throw new InvalidOperationException("File signature does not match the declared file type.");
        }
    }

    private static bool HasValidSignature(string extension, ReadOnlySpan<byte> header)
    {
        if (header.Length == 0)
        {
            return false;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
            ".docx" => header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B,
            ".png" => header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".txt" or ".md" or ".html" or ".htm" => IsTextLike(header),
            _ => false
        };
    }

    private static bool IsTextLike(ReadOnlySpan<byte> header)
    {
        foreach (var value in header)
        {
            if (value == 0)
            {
                return false;
            }
        }

        var preview = Encoding.UTF8.GetString(header);
        return !string.IsNullOrWhiteSpace(preview);
    }
}
