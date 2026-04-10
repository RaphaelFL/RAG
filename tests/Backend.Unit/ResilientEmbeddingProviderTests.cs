using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit;

public class ResilientEmbeddingProviderTests
{
    [Fact]
    public async Task CreateEmbeddingAsync_ShouldFallbackWhenPrimaryFails()
    {
        var provider = new ResilientEmbeddingProvider(
            new ThrowingEmbeddingProvider(),
            new FixedEmbeddingProvider(new[] { 0.1f, 0.2f, 0.3f }),
            new TestSecurityAuditLogger(),
            NullLogger<ResilientEmbeddingProvider>.Instance,
            "python-local",
            "mock");

        var result = await provider.CreateEmbeddingAsync("teste", null, CancellationToken.None);

        result.Should().Equal(0.1f, 0.2f, 0.3f);
    }

    [Fact]
    public async Task CreateEmbeddingAsync_ShouldFallbackWhenPrimaryTimesOutInternally()
    {
        var provider = new ResilientEmbeddingProvider(
            new TimeoutEmbeddingProvider(),
            new FixedEmbeddingProvider(new[] { 0.1f, 0.2f, 0.3f }),
            new TestSecurityAuditLogger(),
            NullLogger<ResilientEmbeddingProvider>.Instance,
            "python-local",
            "mock");

        var result = await provider.CreateEmbeddingAsync("teste", null, CancellationToken.None);

        result.Should().Equal(0.1f, 0.2f, 0.3f);
    }

    [Fact]
    public async Task CreateEmbeddingAsync_ShouldRethrowWhenCallerCancelsRequest()
    {
        var provider = new ResilientEmbeddingProvider(
            new TimeoutEmbeddingProvider(),
            new FixedEmbeddingProvider(new[] { 0.1f, 0.2f, 0.3f }),
            new TestSecurityAuditLogger(),
            NullLogger<ResilientEmbeddingProvider>.Instance,
            "python-local",
            "mock");

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var action = async () => await provider.CreateEmbeddingAsync("teste", null, cancellationTokenSource.Token);

        await action.Should().ThrowAsync<TaskCanceledException>();
    }

    private sealed class ThrowingEmbeddingProvider : IEmbeddingProvider
    {
        public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
            => throw new InvalidOperationException("falha simulada");
    }

    private sealed class TimeoutEmbeddingProvider : IEmbeddingProvider
    {
        public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
            => throw new TaskCanceledException("timeout interno simulado");
    }

    private sealed class FixedEmbeddingProvider : IEmbeddingProvider
    {
        private readonly float[] _embedding;

        public FixedEmbeddingProvider(float[] embedding)
        {
            _embedding = embedding;
        }

        public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
            => Task.FromResult(_embedding);
    }

    private sealed class TestSecurityAuditLogger : ISecurityAuditLogger
    {
        public void LogAuthenticationFailure(string? userId, string reason)
        {
        }

        public void LogAccessDenied(string? userId, string resource)
        {
        }

        public void LogFileRejected(string fileName, string reason)
        {
        }

        public void LogProviderFallback(string provider, string fallbackProvider, string reason)
        {
        }

        public void LogPromptInjectionDetected(string source, string reason)
        {
        }
    }
}