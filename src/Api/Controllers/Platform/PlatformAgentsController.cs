using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformAgentsController : PlatformControllerBase
{
    private readonly IAgentRuntime _agentRuntime;

    public PlatformAgentsController(IAgentRuntime agentRuntime)
    {
        _agentRuntime = agentRuntime;
    }

    [HttpPost("agents/run")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<AgentRunResponseDtoV2>> RunAgent([FromBody] AgentRunRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var result = await _agentRuntime.RunAsync(new AgentRunRequest
        {
            TenantId = GetTenantId(),
            AgentName = request.AgentName,
            Objective = request.Objective,
            ToolBudget = request.ToolBudget,
            Input = request.Input
        }, cancellationToken);

        return Ok(new AgentRunResponseDtoV2
        {
            AgentRunId = result.AgentRunId,
            Status = result.Status,
            Output = result.Output
        });
    }
}