using Chatbot.Api.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Documents;

public abstract class DocumentControllerBase : ControllerBase
{
    protected Guid GetRequiredTenantId()
    {
        var rawTenantId = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(rawTenantId, out var tenantId))
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return tenantId;
    }

    protected ObjectResult CreateValidationError(DocumentUploadValidationFailure validationFailure)
    {
        return StatusCode(validationFailure.StatusCode, new ErrorResponseDto
        {
            Code = validationFailure.Code,
            Message = validationFailure.Message,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected ObjectResult CreateError(int statusCode, string code, string message)
    {
        return StatusCode(statusCode, new ErrorResponseDto
        {
            Code = code,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected ObjectResult CreateAccessDenied(UnauthorizedAccessException exception)
    {
        return CreateError(StatusCodes.Status403Forbidden, "access_denied", exception.Message);
    }

    protected NotFoundObjectResult CreateDocumentNotFound(Guid documentId)
    {
        return NotFound(new ErrorResponseDto
        {
            Code = "document_not_found",
            Message = $"Document {documentId} not found",
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected NotFoundObjectResult CreateChunkEmbeddingNotFound(string chunkId)
    {
        return NotFound(new ErrorResponseDto
        {
            Code = "chunk_embedding_not_found",
            Message = $"Embedding for chunk {chunkId} was not found",
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected ConflictObjectResult CreateDocumentConflict(string message, Guid? existingDocumentId = null)
    {
        return Conflict(new ErrorResponseDto
        {
            Code = "document_conflict",
            Message = message,
            Details = existingDocumentId.HasValue
                ? new Dictionary<string, string[]>
                {
                    ["existingDocumentId"] = [existingDocumentId.Value.ToString()]
                }
                : null,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected BadRequestObjectResult CreateInvalidFile(string message)
    {
        return BadRequest(new ErrorResponseDto
        {
            Code = "invalid_file",
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}