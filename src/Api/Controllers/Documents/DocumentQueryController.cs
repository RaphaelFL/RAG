using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Documents;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
[Produces("application/json")]
public sealed class DocumentQueryController : DocumentControllerBase
{
    private readonly IDocumentQueryService _documentQueryService;

    public DocumentQueryController(IDocumentQueryService documentQueryService)
    {
        _documentQueryService = documentQueryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DocumentDetailsDto>>> ListDocuments(CancellationToken cancellationToken)
    {
        var documents = await _documentQueryService.ListDocumentsAsync(cancellationToken);
        return Ok(documents);
    }

    [HttpGet("{documentId}")]
    [ProducesResponseType(typeof(DocumentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsDto>> GetDocument([FromRoute] Guid documentId, CancellationToken cancellationToken)
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
            return CreateError(StatusCodes.Status403Forbidden, "access_denied", ex.Message);
        }
    }

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
            return CreateError(StatusCodes.Status403Forbidden, "access_denied", ex.Message);
        }
    }

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
            return CreateError(StatusCodes.Status403Forbidden, "access_denied", ex.Message);
        }
    }
}