using Chatbot.Application.Contracts;
using Chatbot.Application.Abstractions;
using Chatbot.Api.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    private readonly IDocumentIngestionService _documentIngestionService;
    private readonly IDocumentReindexService _documentReindexService;
    private readonly IDocumentQueryService _documentQueryService;
    private readonly IDocumentMetadataSuggestionService _documentMetadataSuggestionService;
    private readonly IDocumentUploadValidator _documentUploadValidator;
    private readonly IDocumentUploadCommandFactory _documentUploadCommandFactory;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentIngestionService documentIngestionService,
        IDocumentReindexService documentReindexService,
        IDocumentQueryService documentQueryService,
        IDocumentMetadataSuggestionService documentMetadataSuggestionService,
        IDocumentUploadValidator documentUploadValidator,
        IDocumentUploadCommandFactory documentUploadCommandFactory,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<DocumentsController> logger)
    {
        _documentIngestionService = documentIngestionService;
        _documentReindexService = documentReindexService;
        _documentQueryService = documentQueryService;
        _documentMetadataSuggestionService = documentMetadataSuggestionService;
        _documentUploadValidator = documentUploadValidator;
        _documentUploadCommandFactory = documentUploadCommandFactory;
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

        var validationFailure = _documentUploadValidator.Validate(file);
        if (validationFailure is not null)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, validationFailure.Message);
            return CreateValidationError(validationFailure);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var command = _documentUploadCommandFactory.CreateSuggestionCommand(Guid.NewGuid(), GetRequiredTenantId(), file, stream);
            var suggestion = await _documentMetadataSuggestionService.SuggestAsync(command, cancellationToken);

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
        [FromForm] DocumentUploadFormData formData,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document upload initiated: {filename}", file.FileName);

        var validationFailure = _documentUploadValidator.Validate(file);
        if (validationFailure is not null)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, validationFailure.Message);
            return CreateValidationError(validationFailure);
        }

        try
        {
            var documentId = Guid.NewGuid();
            var tenantId = GetRequiredTenantId();

            using (var stream = file.OpenReadStream())
            {
                var command = _documentUploadCommandFactory.CreateUploadCommand(documentId, tenantId, file, stream, formData);
                var response = await _documentIngestionService.IngestAsync(command, cancellationToken);
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
            var response = await _documentReindexService.ReindexAsync(documentId, request.FullReindex, cancellationToken);
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
            var response = await _documentReindexService.ReindexAsync(request, GetRequiredTenantId(), cancellationToken);
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
    /// Listar os documentos acessiveis para o contexto autenticado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DocumentDetailsDto>>> ListDocuments(CancellationToken cancellationToken)
    {
        var documents = await _documentQueryService.ListDocumentsAsync(cancellationToken);
        return Ok(documents);
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
            var document = await _documentQueryService.GetDocumentAsync(documentId, cancellationToken);
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

    /// <summary>
    /// Consultar os chunks persistidos e o resumo dos embeddings de um documento.
    /// </summary>
    [HttpGet("{documentId}/inspection")]
    [ProducesResponseType(typeof(DocumentInspectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentInspectionDto>> GetDocumentInspection(
        [FromRoute] Guid documentId,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var inspection = await _documentQueryService.GetDocumentInspectionAsync(documentId, search, page, pageSize, cancellationToken);
            if (inspection is null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Code = "document_not_found",
                    Message = $"Document {documentId} not found",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            return Ok(inspection);
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
    /// Consultar o vetor completo de embedding de um chunk especifico sob demanda.
    /// </summary>
    [HttpGet("{documentId}/chunks/{chunkId}/embedding")]
    [ProducesResponseType(typeof(DocumentChunkEmbeddingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentChunkEmbeddingDto>> GetDocumentChunkEmbedding(
        [FromRoute] Guid documentId,
        [FromRoute] string chunkId,
        CancellationToken cancellationToken)
    {
        try
        {
            var embedding = await _documentQueryService.GetDocumentChunkEmbeddingAsync(documentId, chunkId, cancellationToken);
            if (embedding is null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Code = "chunk_embedding_not_found",
                    Message = $"Embedding for chunk {chunkId} was not found",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            return Ok(embedding);
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

    private ActionResult CreateValidationError(DocumentUploadValidationFailure validationFailure)
    {
        return StatusCode(validationFailure.StatusCode, new ErrorResponseDto
        {
            Code = validationFailure.Code,
            Message = validationFailure.Message,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
