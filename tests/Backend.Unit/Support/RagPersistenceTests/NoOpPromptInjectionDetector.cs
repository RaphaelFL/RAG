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

internal sealed class NoOpPromptInjectionDetector : IPromptInjectionDetector
{
    public bool TryDetect(string? input, out string pattern)
    {
        pattern = string.Empty;
        return false;
    }
}
