using Chatbot.Application.Observability;

namespace Chatbot.Application.Services;

public sealed class ChatRequestTemplateResolver : IChatRequestTemplateResolver
{
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public ChatRequestTemplateResolver(
        IPromptTemplateRegistry promptTemplateRegistry,
        IPromptInjectionDetector promptInjectionDetector,
        ISecurityAuditLogger securityAuditLogger)
    {
        _promptTemplateRegistry = promptTemplateRegistry;
        _promptInjectionDetector = promptInjectionDetector;
        _securityAuditLogger = securityAuditLogger;
    }

    public PromptTemplateDefinition Resolve(ChatRequestDto request)
    {
        if (_promptInjectionDetector.TryDetect(request.Message, out var pattern))
        {
            _securityAuditLogger.LogPromptInjectionDetected($"chat:{request.SessionId}", $"Matched blocked pattern '{pattern}'.");
            ChatbotTelemetry.PromptInjectionSignals.Add(1);
            throw new InvalidOperationException("Potential prompt injection detected.");
        }

        var templateId = string.IsNullOrWhiteSpace(request.TemplateId)
            ? ChatDefaults.DefaultTemplateId
            : request.TemplateId;

        var templateVersion = string.IsNullOrWhiteSpace(request.TemplateVersion)
            ? null
            : request.TemplateVersion;

        return _promptTemplateRegistry.GetRequired(templateId, templateVersion);
    }
}