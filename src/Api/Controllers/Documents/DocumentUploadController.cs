using Chatbot.Api.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers.Documents;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
[Produces("application/json")]
public sealed class DocumentUploadController : DocumentControllerBase
{
    private readonly IDocumentIngestionService _documentIngestionService;
    private readonly IDocumentMetadataSuggestionService _documentMetadataSuggestionService;
    private readonly IDocumentUploadValidator _documentUploadValidator;
    private readonly IDocumentUploadCommandFactory _documentUploadCommandFactory;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<DocumentUploadController> _logger;

    public DocumentUploadController(
        IDocumentIngestionService documentIngestionService,
        IDocumentMetadataSuggestionService documentMetadataSuggestionService,
        IDocumentUploadValidator documentUploadValidator,
        IDocumentUploadCommandFactory documentUploadCommandFactory,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<DocumentUploadController> logger)
    {
        _documentIngestionService = documentIngestionService;
        _documentMetadataSuggestionService = documentMetadataSuggestionService;
        _documentUploadValidator = documentUploadValidator;
        _documentUploadCommandFactory = documentUploadCommandFactory;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
    }

    [HttpPost("suggest-metadata")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [ProducesResponseType(typeof(DocumentMetadataSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentMetadataSuggestionDto>> SuggestMetadata(IFormFile file, CancellationToken cancellationToken)
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
            return CreateError(StatusCodes.Status500InternalServerError, "metadata_suggestion_failed", "Failed to analyze document metadata");
        }
    }

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

            using var stream = file.OpenReadStream();
            var command = _documentUploadCommandFactory.CreateUploadCommand(documentId, tenantId, file, stream, formData);
            var response = await _documentIngestionService.IngestAsync(command, cancellationToken);
            return Accepted(response);
        }
        catch (DuplicateDocumentException ex)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, ex.Message);
            return CreateDocumentConflict(ex.Message, ex.ExistingDocumentId);
        }
        catch (InvalidOperationException ex)
        {
            _securityAuditLogger.LogFileRejected(file.FileName, ex.Message);
            return CreateInvalidFile(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return CreateError(StatusCodes.Status500InternalServerError, "upload_failed", "Failed to upload document");
        }
    }
}