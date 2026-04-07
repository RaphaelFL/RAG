namespace Chatbot.Application.Contracts;

public class ChatPolicyDto
{
    public bool Grounded { get; set; }
    public bool HadEnoughEvidence { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "1.0.0";
}
