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

internal sealed class FakeRetrievalService : IRetrievalService
{
    private readonly RetrievalResultDto _result;

    public FakeRetrievalService(RetrievalResultDto result)
    {
        _result = result;
    }

    public Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct) => Task.FromResult(_result);

    public Task<SearchQueryResponseDto> QueryAsync(SearchQueryRequestDto query, CancellationToken ct) =>
        Task.FromResult(new SearchQueryResponseDto());
}
