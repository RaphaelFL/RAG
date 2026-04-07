using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class CurrentStateFileSearchTool : IFileSearchTool
{
    private readonly IRetriever _retriever;

    public CurrentStateFileSearchTool(IRetriever retriever)
    {
        _retriever = retriever;
    }

    public async Task<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken ct)
    {
        var retrieval = await _retriever.RetrieveAsync(new RetrievalPlan
        {
            TenantId = request.TenantId,
            QueryText = request.Query,
            Filters = request.Filters,
            TopK = request.TopK,
            MaxContextChunks = request.TopK,
            UseDenseRetrieval = true,
            UseHybridRetrieval = true,
            UseReranking = true
        }, ct);

        return new FileSearchResult
        {
            Matches = retrieval.Chunks
        };
    }
}
