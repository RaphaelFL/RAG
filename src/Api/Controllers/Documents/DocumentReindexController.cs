using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers.Documents;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
[Produces("application/json")]
public sealed class DocumentReindexController : DocumentControllerBase
{
    private readonly IDocumentReindexService _documentReindexService;
    private readonly ILogger<DocumentReindexController> _logger;

    public DocumentReindexController(
        IDocumentReindexService documentReindexService,
        ILogger<DocumentReindexController> logger)
    {
        _documentReindexService = documentReindexService;
        _logger = logger;
    }

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
        _logger.LogInformation("Reindex initiated for document {documentId}, full: {isFull}", documentId, request.FullReindex);

        try
        {
            var response = await _documentReindexService.ReindexAsync(documentId, request.FullReindex, cancellationToken);
            return Accepted(response);
        }
        catch (KeyNotFoundException)
        {
            return CreateDocumentNotFound(documentId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateAccessDenied(ex);
        }
    }

    [HttpPost("reindex")]
    [Authorize(Policy = "DocumentAdmin")]
    [EnableRateLimiting("reindex")]
    [ProducesResponseType(typeof(BulkReindexResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BulkReindexResponseDto>> ReindexMany([FromBody] BulkReindexRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _documentReindexService.ReindexAsync(request, GetRequiredTenantId(), cancellationToken);
            return Accepted(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateAccessDenied(ex);
        }
    }
}