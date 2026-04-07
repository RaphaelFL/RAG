namespace Chatbot.Application.Services;

public sealed class FileSearchAgentHandler : IGovernedAgentHandler
{
    private readonly IFileSearchTool _fileSearchTool;

    public FileSearchAgentHandler(IFileSearchTool fileSearchTool)
    {
        _fileSearchTool = fileSearchTool;
    }

    public string AgentName => "FileSearchAgent";

    public async Task<AgentToolExecutionResult> ExecuteAsync(AgentRunRequest request, CancellationToken ct)
    {
        var query = request.Input.TryGetValue("query", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        var toolRequest = new FileSearchRequest
        {
            TenantId = request.TenantId,
            Query = query,
            TopK = 5,
            Filters = new Dictionary<string, string[]>()
        };

        var matches = await _fileSearchTool.SearchAsync(toolRequest, ct);
        var outputMatches = matches.Matches.Select(match => new { match.ChunkId, match.DocumentId, match.Score }).ToArray();

        return new AgentToolExecutionResult
        {
            ToolName = "file_search",
            Status = "completed",
            ToolRequest = toolRequest,
            ToolResponse = new { matches = outputMatches },
            Output = new Dictionary<string, object?>
            {
                ["matches"] = outputMatches
            }
        };
    }
}