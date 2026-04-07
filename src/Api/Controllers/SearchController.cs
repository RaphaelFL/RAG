using Chatbot.Application.Contracts;
using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers;

/// <summary>
/// Endpoint para recuperação e busca de documentos
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/search")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly IRetrievalService _retrievalService;
    private readonly ISearchQueryService _searchQueryService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(IRetrievalService retrievalService, ISearchQueryService searchQueryService, ILogger<SearchController> logger)
    {
        _retrievalService = retrievalService;
        _searchQueryService = searchQueryService;
        _logger = logger;
    }

    /// <summary>
    /// Recuperar documentos através de busca híbrida
    /// </summary>
    [HttpPost("retrieve")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(RetrievalResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RetrievalResultDto>> Retrieve(
        [FromBody] RetrievalQueryDto query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieval query: {query}", query.Query);

        try
        {
            var result = await _retrievalService.RetrieveAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_query",
                Message = ex.Message,
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
    /// Executar busca exploratória sem geração de resposta.
    /// </summary>
    [HttpPost("query")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(SearchQueryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SearchQueryResponseDto>> Query(
        [FromBody] SearchQueryRequestDto query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Search query received: {query}", query.Query);

        try
        {
            var result = await _searchQueryService.QueryAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_query",
                Message = ex.Message,
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
}
