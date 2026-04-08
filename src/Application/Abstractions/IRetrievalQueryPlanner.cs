namespace Chatbot.Application.Abstractions;

public interface IRetrievalQueryPlanner
{
    RetrievalExecutionPlan Create(RetrievalQueryDto query);
}