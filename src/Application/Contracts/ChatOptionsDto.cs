namespace Chatbot.Application.Contracts;

public class ChatOptionsDto
{
    public int MaxCitations { get; set; } = 5;
    public bool AllowGeneralKnowledge { get; set; } = true;
    public bool SemanticRanking { get; set; } = true;
}
