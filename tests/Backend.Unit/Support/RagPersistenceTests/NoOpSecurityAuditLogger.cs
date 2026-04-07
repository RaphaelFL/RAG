using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.RagPersistenceTestsSupport;

internal sealed class NoOpSecurityAuditLogger : ISecurityAuditLogger
{
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
    }

    public void LogProviderFallback(string provider, string fallbackProvider, string reason)
    {
    }
}
