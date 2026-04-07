using Chatbot.Application.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformEmbeddingsController : PlatformControllerBase
{
    private readonly IEmbeddingGenerationService _embeddingGenerationService;
    private readonly EmbeddingGenerationOptions _embeddingOptions;

    public PlatformEmbeddingsController(
        IEmbeddingGenerationService embeddingGenerationService,
        IOptions<EmbeddingGenerationOptions> embeddingOptions)
    {
        _embeddingGenerationService = embeddingGenerationService;
        _embeddingOptions = embeddingOptions.Value;
    }

    [HttpPost("embeddings/generate")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<GenerateEmbeddingsResponseDtoV2>> GenerateEmbeddings([FromBody] GenerateEmbeddingsRequestDtoV2 request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var inputs = request.Items.Select(item => new EmbeddingInput
        {
            ChunkId = string.IsNullOrWhiteSpace(item.ChunkId) ? Guid.NewGuid().ToString("N") : item.ChunkId,
            DocumentId = item.DocumentId,
            TenantId = tenantId,
            ContentHash = string.IsNullOrWhiteSpace(item.ContentHash) ? ComputeHash(item.Text) : item.ContentHash,
            Text = item.Text
        }).ToArray();

        var result = await _embeddingGenerationService.GenerateBatchAsync(new EmbeddingBatchRequest
        {
            EmbeddingModelName = request.EmbeddingModelName ?? _embeddingOptions.ModelName,
            EmbeddingModelVersion = request.EmbeddingModelVersion ?? _embeddingOptions.ModelVersion,
            Inputs = inputs
        }, cancellationToken);

        return Ok(new GenerateEmbeddingsResponseDtoV2
        {
            ModelName = request.EmbeddingModelName ?? _embeddingOptions.ModelName,
            ModelVersion = request.EmbeddingModelVersion ?? _embeddingOptions.ModelVersion,
            Dimensions = result.FirstOrDefault()?.VectorDimensions ?? _embeddingOptions.Dimensions,
            Items = result.Select(item => new GenerateEmbeddingItemResponseDtoV2
            {
                ChunkId = item.ChunkId,
                VectorDimensions = item.VectorDimensions,
                Vector = item.Vector
            }).ToList()
        });
    }
}