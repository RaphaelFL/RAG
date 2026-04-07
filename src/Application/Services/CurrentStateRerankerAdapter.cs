using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class CurrentStateRerankerAdapter : IReranker
{
    public Task<IReadOnlyCollection<RetrievedChunk>> RerankAsync(RerankRequest request, CancellationToken ct)
    {
        var ordered = request.Candidates
            .OrderByDescending(candidate => candidate.Score)
            .Take(request.TopK)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<RetrievedChunk>>(ordered);
    }
}
