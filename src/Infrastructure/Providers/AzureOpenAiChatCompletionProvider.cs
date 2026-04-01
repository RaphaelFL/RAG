using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class AzureOpenAiChatCompletionProvider : IChatCompletionProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChatModelOptions _chatModelOptions;
    private readonly ExternalProviderClientOptions _providerOptions;

    public AzureOpenAiChatCompletionProvider(
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
        var deployment = ResolveChatDeployment();
        var client = _httpClientFactory.CreateClient("AzureOpenAI");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={Uri.EscapeDataString(_providerOptions.AzureOpenAiApiVersion)}");
        message.Headers.TryAddWithoutValidation("api-key", _providerOptions.AzureOpenAiApiKey);
        message.Content = JsonContent.Create(new
        {
            messages = BuildMessages(request),
            temperature = _chatModelOptions.Temperature,
            max_tokens = _chatModelOptions.MaxTokens,
            top_p = _chatModelOptions.TopP
        });

        using var response = await client.SendAsync(message, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure OpenAI chat completion failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var content = ExtractContent(root);
        var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;

        return new ChatCompletionResult
        {
            Message = content,
            Model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? _chatModelOptions.Model
                : _chatModelOptions.Model,
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

    private string ResolveChatDeployment()
    {
        return string.IsNullOrWhiteSpace(_providerOptions.AzureOpenAiChatDeployment)
            ? _chatModelOptions.Deployment
            : _providerOptions.AzureOpenAiChatDeployment;
    }

    private static object[] BuildMessages(ChatCompletionRequest request)
    {
        var groundedInstructions = request.AllowGeneralKnowledge
            ? "Responda em portugues do Brasil. Se houver contexto documental, priorize-o. Se nao houver, pode responder com conhecimento geral, deixando isso implicito na resposta."
            : "Responda em portugues do Brasil. Use apenas o contexto documental fornecido. Se o contexto for insuficiente, seja explicito e nao invente informacoes.";

        var context = request.RetrievedChunks.Count == 0
            ? ""
            : string.Join("\n\n", request.RetrievedChunks.Select((chunk, index) => $"[Fonte {index + 1}] Documento: {chunk.DocumentTitle} | ChunkId: {chunk.ChunkId} | Conteudo: {chunk.Content}"));

        var userMessage = string.IsNullOrWhiteSpace(context)
            ? request.Message
            : $"Pergunta do usuario:\n{request.Message}\n\nContexto documental:\n{context}";

        return new object[]
        {
            new
            {
                role = "system",
                content = $"Template: {request.Template.TemplateId} v{request.Template.Version}. {groundedInstructions}"
            },
            new
            {
                role = "user",
                content = userMessage
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