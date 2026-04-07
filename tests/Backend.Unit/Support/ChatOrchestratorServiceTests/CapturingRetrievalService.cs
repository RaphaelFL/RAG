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

internal sealed class CapturingRetrievalService : IRetrievalService
{
    private readonly RetrievalResultDto _result;

    public CapturingRetrievalService(RetrievalResultDto result)
    {
        _result = result;
    }

    public RetrievalQueryDto? LastQuery { get; private set; }

    public Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct)
    {
        LastQuery = query;
        return Task.FromResult(_result);
    }
}
