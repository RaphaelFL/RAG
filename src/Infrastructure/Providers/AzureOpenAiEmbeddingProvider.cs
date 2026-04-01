using System.Net.Http.Json;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class AzureOpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly ExternalProviderClientOptions _providerOptions;

    public AzureOpenAiEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<ExternalProviderClientOptions> providerOptions)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingOptions = embeddingOptions.Value;
        _providerOptions = providerOptions.Value;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        var deployment = ResolveEmbeddingDeployment();
        var client = _httpClientFactory.CreateClient("AzureOpenAI");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/openai/deployments/{Uri.EscapeDataString(deployment)}/embeddings?api-version={Uri.EscapeDataString(_providerOptions.AzureOpenAiApiVersion)}");
        message.Headers.TryAddWithoutValidation("api-key", _providerOptions.AzureOpenAiApiKey);
        message.Content = JsonContent.Create(new
        {
            input = text,
            model = string.IsNullOrWhiteSpace(modelOverride) ? _embeddingOptions.Model : modelOverride
        });

        using var response = await client.SendAsync(message, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure OpenAI embedding generation failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
        {
            return Array.Empty<float>();
        }

        return data[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(item => item.GetSingle())
            .ToArray();
    }

    private string ResolveEmbeddingDeployment()
    {
        return string.IsNullOrWhiteSpace(_providerOptions.AzureOpenAiEmbeddingDeployment)
            ? _embeddingOptions.Deployment
            : _providerOptions.AzureOpenAiEmbeddingDeployment;
    }
}