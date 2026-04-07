using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformPromptAssemblyController : PlatformControllerBase
{
    private readonly IPromptAssembler _promptAssembler;

    public PlatformPromptAssemblyController(IPromptAssembler promptAssembler)
    {
        _promptAssembler = promptAssembler;
    }

    [HttpPost("prompt-assembly")]
    public async Task<ActionResult<PromptAssemblyResponseDto>> AssemblePrompt([FromBody] PromptAssemblyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _promptAssembler.AssembleAsync(new PromptAssemblyRequest
        {
            TenantId = GetTenantId(),
            SystemInstructions = request.SystemInstructions,
            UserQuestion = request.Question,
            MaxPromptTokens = request.MaxPromptTokens,
            AllowGeneralKnowledge = request.AllowGeneralKnowledge,
            Chunks = request.Chunks.Select(chunk => new RetrievedChunk
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Text = chunk.Text,
                Score = chunk.Score,
                Metadata = chunk.Metadata
            }).ToArray()
        }, cancellationToken);

        return Ok(new PromptAssemblyResponseDto
        {
            Prompt = result.Prompt,
            EstimatedPromptTokens = result.EstimatedPromptTokens,
            IncludedChunkIds = result.IncludedChunkIds.ToList(),
            Citations = result.HumanReadableCitations.ToList()
        });
    }
}