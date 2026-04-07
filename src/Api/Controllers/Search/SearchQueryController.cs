using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers.Search;

[ApiController]
[Authorize]
[Route("api/v1/search")]
[Produces("application/json")]
public sealed class SearchQueryController : SearchControllerBase
{
    private readonly ISearchQueryService _searchQueryService;
    private readonly ILogger<SearchQueryController> _logger;

    public SearchQueryController(ISearchQueryService searchQueryService, ILogger<SearchQueryController> logger)
    {
        _searchQueryService = searchQueryService;
        _logger = logger;
    }

    [HttpPost("query")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(SearchQueryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SearchQueryResponseDto>> Query([FromBody] SearchQueryRequestDto query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Search query received: {query}", query.Query);

        try
        {
            var result = await _searchQueryService.QueryAsync(query, cancellationToken);
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