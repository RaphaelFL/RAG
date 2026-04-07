using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Xunit;

using Backend.Unit.SearchQueryServiceTestsSupport;

namespace Backend.Unit;

public class SearchQueryServiceTests
{
	[Fact]
	public async Task QueryAsync_ShouldMapRetrievedChunks_ToSearchItems()
	{
		var sut = new SearchQueryService(new FakeRetrievalService(new RetrievalResultDto
		{
			Chunks = new List<RetrievedChunkDto>
			{
				new()
				{
					ChunkId = "chunk-001",
					DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
					Content = "Conteudo relevante sobre reembolso.",
					Score = 0.91,
					DocumentTitle = "Politica Financeira"
				}
			}
		}));

		var result = await sut.QueryAsync(new SearchQueryRequestDto
		{
			Query = "reembolso",
			Top = 3
		}, CancellationToken.None);

		result.Count.Should().Be(1);
		result.Items.Should().ContainSingle();
		result.Items[0].Title.Should().Be("Politica Financeira");
		result.Items[0].ChunkId.Should().Be("chunk-001");
	}
}