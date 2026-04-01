using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Chatbot.Api.Controllers;

[ApiController]
[Authorize(Policy = "McpAccess")]
[Route("mcp")]
[Produces("application/json")]
public sealed class McpController : ControllerBase
{
    private readonly IMcpServer _mcpServer;
    private readonly FeatureFlagOptions _featureFlags;

    public McpController(IMcpServer mcpServer, IOptions<FeatureFlagOptions> featureFlags)
    {
        _mcpServer = mcpServer;
        _featureFlags = featureFlags.Value;
    }

    [HttpPost]
    [ProducesResponseType(typeof(JsonRpcResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JsonRpcResponse>> Handle([FromBody] JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (!_featureFlags.EnableMcp)
        {
            return NotFound(new ErrorResponseDto
            {
                Code = "feature_disabled",
                Message = "MCP is disabled for this environment.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var response = await _mcpServer.HandleAsync(request, User, cancellationToken);
        return Ok(response);
    }
}