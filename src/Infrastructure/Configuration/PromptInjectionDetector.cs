using Chatbot.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Configuration;

public sealed class PromptInjectionDetector : IPromptInjectionDetector
{
    private readonly PromptTemplateOptions _options;

    public PromptInjectionDetector(IOptions<PromptTemplateOptions> options)
    {
        _options = options.Value;
    }

    public bool TryDetect(string? input, out string pattern)
    {
        pattern = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        foreach (var candidate in _options.BlockedInputPatterns)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && input.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                pattern = candidate;
                return true;
            }
        }

        return false;
    }
}