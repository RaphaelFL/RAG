using Chatbot.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Configuration;

public sealed class PromptTemplateRegistry : IPromptTemplateRegistry
{
    private readonly PromptTemplateOptions _options;
    private readonly IReadOnlyDictionary<string, PromptTemplateDefinition> _templates;

    public PromptTemplateRegistry(IOptions<PromptTemplateOptions> options)
    {
        _options = options.Value;
        _templates = new Dictionary<string, PromptTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["grounded_answer"] = new PromptTemplateDefinition
            {
                TemplateId = "grounded_answer",
                Version = _options.GroundedAnswerVersion,
                InsufficientEvidenceMessage = _options.InsufficientEvidenceMessage
            },
            ["comparative_answer"] = new PromptTemplateDefinition
            {
                TemplateId = "comparative_answer",
                Version = _options.GroundedAnswerVersion,
                InsufficientEvidenceMessage = _options.InsufficientEvidenceMessage
            },
            ["document_summary"] = new PromptTemplateDefinition
            {
                TemplateId = "document_summary",
                Version = _options.GroundedAnswerVersion,
                InsufficientEvidenceMessage = _options.InsufficientEvidenceMessage
            }
        };
    }

    public PromptTemplateDefinition GetRequired(string templateId, string? templateVersion = null)
    {
        if (!_templates.TryGetValue(templateId, out var template))
        {
            throw new InvalidOperationException($"Unsupported template '{templateId}'.");
        }

        var expectedVersion = template.Version;
        if (!string.IsNullOrWhiteSpace(templateVersion) && !string.Equals(templateVersion, expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported template version '{templateVersion}' for template '{templateId}'. Expected '{expectedVersion}'.");
        }

        return template;
    }

    public IReadOnlyCollection<PromptTemplateDefinition> ListAll()
    {
        return _templates.Values
            .OrderBy(template => template.TemplateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}