using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;
using InfraCfg = Chatbot.Infrastructure.Configuration;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformCapabilitiesController : PlatformControllerBase
{
    private readonly AppCfg.EmbeddingGenerationOptions _embeddingOptions;
    private readonly AppCfg.VectorStoreOptions _vectorStoreOptions;
    private readonly AppCfg.AgentRuntimeOptions _agentRuntimeOptions;
    private readonly InfraCfg.FeatureFlagOptions _featureFlags;

    public PlatformCapabilitiesController(
        IOptions<AppCfg.EmbeddingGenerationOptions> embeddingOptions,
        IOptions<AppCfg.VectorStoreOptions> vectorStoreOptions,
        IOptions<AppCfg.AgentRuntimeOptions> agentRuntimeOptions,
        IOptions<InfraCfg.FeatureFlagOptions> featureFlags)
    {
        _embeddingOptions = embeddingOptions.Value;
        _vectorStoreOptions = vectorStoreOptions.Value;
        _agentRuntimeOptions = agentRuntimeOptions.Value;
        _featureFlags = featureFlags.Value;
    }

    [HttpGet("capabilities")]
    public ActionResult<object> GetCapabilities()
    {
        return Ok(new
        {
            mcpEnabled = _featureFlags.EnableMcp,
            semanticRankingEnabled = _featureFlags.EnableSemanticRanking,
            graphRagEnabled = _featureFlags.EnableGraphRag,
            vectorStore = _vectorStoreOptions.Provider,
            embeddingModel = new { _embeddingOptions.ModelName, _embeddingOptions.ModelVersion, _embeddingOptions.Dimensions },
            agentRuntime = new { _agentRuntimeOptions.Enabled, _agentRuntimeOptions.MaxToolBudget, _agentRuntimeOptions.MaxDepth }
        });
    }
}