namespace Chatbot.Application.Abstractions;

public interface IAgenticChatPlanner
{
    AgenticChatPlan CreatePlan(ChatRequestDto request);
}