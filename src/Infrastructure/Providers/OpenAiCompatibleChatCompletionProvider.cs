using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class OpenAiCompatibleChatCompletionProvider : IChatCompletionProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChatModelOptions _chatModelOptions;
    private readonly ExternalProviderClientOptions _providerOptions;

    public OpenAiCompatibleChatCompletionProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatModelOptions> chatModelOptions,
        IOptions<ExternalProviderClientOptions> providerOptions)
    {
        _httpClientFactory = httpClientFactory;
        _chatModelOptions = chatModelOptions.Value;
        _providerOptions = providerOptions.Value;
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("OpenAICompatible");
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        ApplyAuthentication(message);
        message.Content = JsonContent.Create(new
        {
            model = _providerOptions.ResolveOpenAiCompatibleChatModel(_chatModelOptions.Model),
            messages = BuildMessages(request),
            temperature = _chatModelOptions.Temperature,
            max_tokens = _chatModelOptions.MaxTokens,
            top_p = _chatModelOptions.TopP,
            stream = false
        });

        using var response = await client.SendAsync(message, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI-compatible chat completion failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;

        return new ChatCompletionResult
        {
            Message = ExtractContent(root),
            Model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? _providerOptions.ResolveOpenAiCompatibleChatModel(_chatModelOptions.Model)
                : _providerOptions.ResolveOpenAiCompatibleChatModel(_chatModelOptions.Model),
            PromptTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("prompt_tokens", out var promptTokens)
                ? promptTokens.GetInt32()
                : 0,
            CompletionTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("completion_tokens", out var completionTokens)
                ? completionTokens.GetInt32()
                : 0,
            TotalTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("total_tokens", out var totalTokens)
                ? totalTokens.GetInt32()
                : 0
        };
    }

    private void ApplyAuthentication(HttpRequestMessage message)
    {
        if (ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.OpenAiCompatibleApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _providerOptions.OpenAiCompatibleApiKey);
        }
    }

    private object[] BuildMessages(ChatCompletionRequest request)
    {
        return new object[]
        {
            new
            {
                role = "system",
                content = GroundedChatPromptComposer.BuildSystemPrompt(request)
            },
            new
            {
                role = "user",
                content = GroundedChatPromptComposer.BuildUserPrompt(request, _chatModelOptions.MaxPromptContextTokens)
            }
        };
    }

    private static string ExtractContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var contentElement))
        {
            return string.Empty;
        }

        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString() ?? string.Empty;
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in contentElement.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var textElement))
                {
                    builder.Append(textElement.GetString());
                }
            }

            return builder.ToString();
        }

        return string.Empty;
    }
}