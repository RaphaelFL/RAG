using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly ExternalProviderClientOptions _providerOptions;

    public OpenAiCompatibleEmbeddingProvider(
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
        var client = _httpClientFactory.CreateClient("OpenAICompatible");
        using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings");
        ApplyAuthentication(message);
        message.Content = JsonContent.Create(new
        {
            input = text,
            model = string.IsNullOrWhiteSpace(modelOverride)
                ? _providerOptions.ResolveOpenAiCompatibleEmbeddingModel(_embeddingOptions.Model)
                : modelOverride
        });

        using var response = await client.SendAsync(message, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI-compatible embedding generation failed with status {(int)response.StatusCode}: {payload}");
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

    private void ApplyAuthentication(HttpRequestMessage message)
    {
        if (ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.OpenAiCompatibleApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _providerOptions.OpenAiCompatibleApiKey);
        }
    }
}