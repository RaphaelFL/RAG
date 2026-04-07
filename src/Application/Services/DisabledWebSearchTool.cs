using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class DisabledWebSearchTool : IWebSearchTool
{
    public Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct)
    {
        return Task.FromResult(new WebSearchResult
        {
            Hits = Array.Empty<WebSearchHit>()
        });
    }
}
