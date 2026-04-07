using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformRetrievalController : PlatformControllerBase
{
    private readonly IRetriever _retriever;

    public PlatformRetrievalController(IRetriever retriever)
    {
        _retriever = retriever;
    }

    [HttpPost("retrieval/query")]
    public async Task<ActionResult<RetrievalResponseDto>> QueryRetrieval([FromBody] RetrievalRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _retriever.RetrieveAsync(new RetrievalPlan
        {
            TenantId = GetTenantId(),
            QueryText = request.Query,
            TopK = request.TopK,
            MaxContextChunks = request.TopK,
            UseDenseRetrieval = true,
            UseHybridRetrieval = request.UseHybridRetrieval,
            UseReranking = request.UseReranking,
            Filters = request.Filters
        }, cancellationToken);

        return Ok(new RetrievalResponseDto
        {
            Strategy = result.RetrievalStrategy,
            LatencyMs = result.LatencyMs,
            Chunks = result.Chunks.Select(chunk => new RetrievedChunkDtoV2
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Text = chunk.Text,
                Score = chunk.Score,
                SourceName = chunk.Metadata.TryGetValue("documentTitle", out var title) ? title : string.Empty,
                Metadata = chunk.Metadata
            }).ToList()
        });
    }
}