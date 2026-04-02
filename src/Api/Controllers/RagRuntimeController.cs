using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Authorize(Policy = "DocumentAdmin")]
[Route("api/v1/admin/rag-runtime")]
[Produces("application/json")]
public sealed class RagRuntimeController : ControllerBase
{
    private readonly IRagRuntimeAdministrationService _runtimeAdministrationService;

    public RagRuntimeController(IRagRuntimeAdministrationService runtimeAdministrationService)
    {
        _runtimeAdministrationService = runtimeAdministrationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RagRuntimeSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<RagRuntimeSettingsDto> Get()
    {
        return Ok(_runtimeAdministrationService.GetSettings());
    }

    [HttpPut]
    [ProducesResponseType(typeof(RagRuntimeSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public ActionResult<RagRuntimeSettingsDto> Update([FromBody] UpdateRagRuntimeSettingsDto request)
    {
        if (request.DenseChunkSize <= 0 || request.NarrativeChunkSize <= 0 || request.MinimumChunkCharacters <= 0)
        {
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_runtime_settings",
                Message = "Chunking e thresholds devem ser maiores que zero.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (request.RetrievalCacheTtlSeconds <= 0 || request.ChatCompletionCacheTtlSeconds <= 0 || request.EmbeddingCacheTtlHours <= 0)
        {
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_runtime_settings",
                Message = "TTLs devem ser maiores que zero.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(_runtimeAdministrationService.UpdateSettings(request));
    }
}