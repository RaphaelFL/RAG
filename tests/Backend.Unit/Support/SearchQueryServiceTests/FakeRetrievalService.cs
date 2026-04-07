using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Backend.Unit.SearchQueryServiceTestsSupport;

internal sealed class FakeRetrievalService : IRetrievalService
{
	private readonly RetrievalResultDto _result;

	public FakeRetrievalService(RetrievalResultDto result)
	{
		_result = result;
	}

	public Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct)
	{
		return Task.FromResult(_result);
	}
}