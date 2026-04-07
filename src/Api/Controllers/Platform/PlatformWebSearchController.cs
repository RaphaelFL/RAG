using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformWebSearchController : PlatformControllerBase
{
    private readonly IWebSearchTool _webSearchTool;

    public PlatformWebSearchController(IWebSearchTool webSearchTool)
    {
        _webSearchTool = webSearchTool;
    }

    [HttpPost("web-search")]
    public async Task<ActionResult<WebSearchResponseDtoV2>> WebSearch([FromBody] WebSearchRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var result = await _webSearchTool.SearchAsync(new WebSearchRequest
        {
            TenantId = GetTenantId(),
            Query = request.Query,
            TopK = request.TopK
        }, cancellationToken);

        return Ok(new WebSearchResponseDtoV2
        {
            Hits = result.Hits.Select(hit => new WebSearchHitDtoV2
            {
                Title = hit.Title,
                Url = hit.Url,
                Snippet = hit.Snippet,
                Score = hit.Score
            }).ToList()
        });
    }
}