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

internal sealed class CountingEmbeddingProvider : IEmbeddingProvider
{
    public int CallCount { get; private set; }

    public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new[] { 0.2f, 0.3f, 0.4f });
    }
}
