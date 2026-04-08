namespace Chatbot.Infrastructure.Persistence;

internal sealed class OperationalAuditFileSet
{
    public string RetrievalLogPath { get; }
    public string PromptAssemblyLogPath { get; }
    public string AgentRunLogPath { get; }
    public string ToolExecutionLogPath { get; }

    private OperationalAuditFileSet(string auditPath)
    {
        RetrievalLogPath = Path.Combine(auditPath, "retrieval-log.jsonl");
        PromptAssemblyLogPath = Path.Combine(auditPath, "prompt-assembly-log.jsonl");
        AgentRunLogPath = Path.Combine(auditPath, "agent-run-log.jsonl");
        ToolExecutionLogPath = Path.Combine(auditPath, "tool-execution-log.jsonl");
    }

    public static OperationalAuditFileSet Create(string configuredPath, string contentRootPath)
    {
        var basePath = ResolveBasePath(configuredPath, contentRootPath);
        var auditPath = Path.Combine(basePath, "operational-audit");
        Directory.CreateDirectory(auditPath);
        return new OperationalAuditFileSet(auditPath);
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}