namespace Chatbot.Application.Services;

public sealed class PromptAssemblyAgentHandler : IGovernedAgentHandler
{
    private readonly IPromptAssembler _promptAssembler;

    public PromptAssemblyAgentHandler(IPromptAssembler promptAssembler)
    {
        _promptAssembler = promptAssembler;
    }

    public string AgentName => "PromptAssemblyAgent";

    public async Task<AgentToolExecutionResult> ExecuteAsync(AgentRunRequest request, CancellationToken ct)
    {
        var question = request.Input.TryGetValue("question", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        var toolRequest = new PromptAssemblyRequest
        {
            TenantId = request.TenantId,
            UserQuestion = question,
            SystemInstructions = "Monte um prompt grounded, seguro e auditavel.",
            Chunks = Array.Empty<RetrievedChunk>(),
            MaxPromptTokens = 4000,
            AllowGeneralKnowledge = false
        };

        var prompt = await _promptAssembler.AssembleAsync(toolRequest, ct);
        return new AgentToolExecutionResult
        {
            ToolName = "assemble_prompt",
            Status = "completed",
            ToolRequest = toolRequest,
            ToolResponse = new { prompt.Prompt, prompt.EstimatedPromptTokens, prompt.IncludedChunkIds },
            Output = new Dictionary<string, object?>
            {
                ["prompt"] = prompt.Prompt
            }
        };
    }
}