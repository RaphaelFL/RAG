namespace Chatbot.Application.Abstractions;

public interface IOperationalAuditReader : IRetrievalAuditReader, IPromptAssemblyAuditReader, IAgentRunAuditReader, IToolExecutionAuditReader, IOperationalAuditFeedReader
{
}