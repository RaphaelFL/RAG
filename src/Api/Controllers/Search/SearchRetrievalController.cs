using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers.Search;

[ApiController]
[Authorize]
[Route("api/v1/search")]
[Produces("application/json")]
public sealed class SearchRetrievalController : SearchControllerBase
{
    private readonly IRetrievalService _retrievalService;
    private readonly ILogger<SearchRetrievalController> _logger;

    public SearchRetrievalController(IRetrievalService retrievalService, ILogger<SearchRetrievalController> logger)
    {
        _retrievalService = retrievalService;
        _logger = logger;
    }

    [HttpPost("retrieve")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(RetrievalResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RetrievalResultDto>> Retrieve([FromBody] RetrievalQueryDto query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieval query: {query}", query.Query);

        try
        {
            var result = await _retrievalService.RetrieveAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateError(StatusCodes.Status400BadRequest, "invalid_query", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateError(StatusCodes.Status403Forbidden, "access_denied", ex.Message);
        }
    }
}