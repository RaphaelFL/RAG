using System.Net.Http.Json;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Authentication;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class AzureOpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly ExternalProviderClientOptions _providerOptions;
    private readonly IAzureAccessTokenProvider _azureAccessTokenProvider;

    public AzureOpenAiEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<ExternalProviderClientOptions> providerOptions,
        IAzureAccessTokenProvider azureAccessTokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingOptions = embeddingOptions.Value;
        _providerOptions = providerOptions.Value;
        _azureAccessTokenProvider = azureAccessTokenProvider;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        var deployment = ResolveEmbeddingDeployment();
        var client = _httpClientFactory.CreateClient("AzureOpenAI");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/openai/deployments/{Uri.EscapeDataString(deployment)}/embeddings?api-version={Uri.EscapeDataString(_providerOptions.AzureOpenAiApiVersion)}");
        await ApplyAzureOpenAiAuthenticationAsync(message, ct);
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

    private async Task ApplyAzureOpenAiAuthenticationAsync(HttpRequestMessage message, CancellationToken ct)
    {
        if (ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureOpenAiApiKey))
        {
            message.Headers.TryAddWithoutValidation("api-key", _providerOptions.AzureOpenAiApiKey);
            return;
        }

        var token = await _azureAccessTokenProvider.GetTokenAsync("https://cognitiveservices.azure.com/.default", ct);
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}