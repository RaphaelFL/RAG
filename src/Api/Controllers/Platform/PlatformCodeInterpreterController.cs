using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformCodeInterpreterController : PlatformControllerBase
{
    private readonly ICodeInterpreter _codeInterpreter;

    public PlatformCodeInterpreterController(ICodeInterpreter codeInterpreter)
    {
        _codeInterpreter = codeInterpreter;
    }

    [HttpPost("code-interpreter")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<CodeInterpreterResponseDtoV2>> RunCode([FromBody] CodeInterpreterRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var result = await _codeInterpreter.ExecuteAsync(new CodeInterpreterRequest
        {
            TenantId = GetTenantId(),
            Language = request.Language,
            Code = request.Code,
            InputArtifacts = request.InputArtifacts
        }, cancellationToken);

        return Ok(new CodeInterpreterResponseDtoV2
        {
            ExitCode = result.ExitCode,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
            OutputArtifacts = result.OutputArtifacts.ToList()
        });
    }
}