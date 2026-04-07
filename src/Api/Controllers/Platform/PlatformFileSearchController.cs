using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformFileSearchController : PlatformControllerBase
{
    private readonly IFileSearchTool _fileSearchTool;

    public PlatformFileSearchController(IFileSearchTool fileSearchTool)
    {
        _fileSearchTool = fileSearchTool;
    }

    [HttpPost("file-search")]
    public async Task<ActionResult<FileSearchResult>> FileSearch([FromBody] RetrievalRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = GetTenantId(),
            Query = request.Query,
            TopK = request.TopK,
            Filters = request.Filters
        }, cancellationToken);

        return Ok(result);
    }
}