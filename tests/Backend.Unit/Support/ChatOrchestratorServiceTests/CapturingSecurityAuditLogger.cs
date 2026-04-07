using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.ChatOrchestratorServiceTestsSupport;

internal sealed class CapturingSecurityAuditLogger : ISecurityAuditLogger
{
    public string? LastPromptInjectionSource { get; private set; }

    public void LogAccessDenied(string? userId, string resource)
    {
    }

    public void LogAuthenticationFailure(string? userId, string reason)
    {
    }

    public void LogFileRejected(string fileName, string reason)
    {
    }

    public void LogPromptInjectionDetected(string source, string reason)
    {
        LastPromptInjectionSource = source;
    }

    public void LogProviderFallback(string provider, string fallbackProvider, string reason)
    {
    }
}
