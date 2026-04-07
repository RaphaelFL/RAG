namespace Chatbot.Application.Services;

public sealed class WebSearchAgentHandler : IGovernedAgentHandler
{
    private readonly IWebSearchTool _webSearchTool;

    public WebSearchAgentHandler(IWebSearchTool webSearchTool)
    {
        _webSearchTool = webSearchTool;
    }

    public string AgentName => "WebSearchAgent";

    public async Task<AgentToolExecutionResult> ExecuteAsync(AgentRunRequest request, CancellationToken ct)
    {
        var query = request.Input.TryGetValue("query", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        var toolRequest = new WebSearchRequest
        {
            TenantId = request.TenantId,
            Query = query,
            TopK = 5
        };

        var hits = await _webSearchTool.SearchAsync(toolRequest, ct);
        return new AgentToolExecutionResult
        {
            ToolName = "web_search",
            Status = "completed",
            ToolRequest = toolRequest,
            ToolResponse = new { hits = hits.Hits },
            Output = new Dictionary<string, object?>
            {
                ["hits"] = hits.Hits.ToArray()
            }
        };
    }
}