using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Authentication;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class AzureOpenAiChatCompletionProvider : IChatCompletionProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChatModelOptions _chatModelOptions;
    private readonly ExternalProviderClientOptions _providerOptions;
    private readonly IAzureAccessTokenProvider _azureAccessTokenProvider;

    public AzureOpenAiChatCompletionProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatModelOptions> chatModelOptions,
        IOptions<ExternalProviderClientOptions> providerOptions,
        IAzureAccessTokenProvider azureAccessTokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _chatModelOptions = chatModelOptions.Value;
        _providerOptions = providerOptions.Value;
        _azureAccessTokenProvider = azureAccessTokenProvider;
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var deployment = ResolveChatDeployment();
        var client = _httpClientFactory.CreateClient("AzureOpenAI");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={Uri.EscapeDataString(_providerOptions.AzureOpenAiApiVersion)}");
        await ApplyAzureOpenAiAuthenticationAsync(message, ct);
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

    private object[] BuildMessages(ChatCompletionRequest request)
    {
        var groundedInstructions = request.AllowGeneralKnowledge
            ? "Responda em portugues do Brasil. Se houver contexto documental, priorize-o. Se nao houver, pode responder com conhecimento geral, deixando isso implicito na resposta."
            : "Responda em portugues do Brasil. Use apenas o contexto documental fornecido. Se o contexto for insuficiente, seja explicito e nao invente informacoes.";

        var context = BuildContextBlock(request.Message, request.RetrievedChunks);

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

    private string BuildContextBlock(string message, IReadOnlyCollection<RetrievedChunkDto> chunks)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var reservedTokens = EstimateTokens(message) + 256;
        var remainingBudget = Math.Max(0, _chatModelOptions.MaxPromptContextTokens - reservedTokens);
        if (remainingBudget <= 0)
        {
            return string.Empty;
        }

        var blocks = new List<string>(chunks.Count);
        var consumedTokens = 0;
        var sourceNumber = 1;

        foreach (var chunk in chunks)
        {
            var header = BuildChunkHeader(chunk, sourceNumber);
            var headerTokens = EstimateTokens(header);
            var contentBudget = remainingBudget - consumedTokens - headerTokens;
            if (contentBudget <= 24)
            {
                break;
            }

            var trimmedContent = TrimToTokenBudget(chunk.Content, contentBudget);
            if (string.IsNullOrWhiteSpace(trimmedContent))
            {
                continue;
            }

            var block = $"{header}\nConteudo: {trimmedContent}";
            var blockTokens = EstimateTokens(block);
            if (blockTokens > remainingBudget - consumedTokens)
            {
                break;
            }

            blocks.Add(block);
            consumedTokens += blockTokens;
            sourceNumber++;

            if (consumedTokens >= remainingBudget)
            {
                break;
            }
        }

        return string.Join("\n\n", blocks);
    }

    private static string BuildChunkHeader(RetrievedChunkDto chunk, int sourceNumber)
    {
        var location = chunk.PageNumber > 0
            ? chunk.EndPageNumber > chunk.PageNumber
                ? $"Paginas {chunk.PageNumber}-{chunk.EndPageNumber}"
                : $"Pagina {chunk.PageNumber}"
            : "Localizacao nao informada";

        var section = string.IsNullOrWhiteSpace(chunk.Section)
            ? string.Empty
            : $" | Secao: {chunk.Section}";

        return $"[Fonte {sourceNumber}] Documento: {chunk.DocumentTitle} | ChunkId: {chunk.ChunkId} | {location}{section}";
    }

    private static string TrimToTokenBudget(string content, int tokenBudget)
    {
        if (string.IsNullOrWhiteSpace(content) || tokenBudget <= 0)
        {
            return string.Empty;
        }

        if (EstimateTokens(content) <= tokenBudget)
        {
            return content;
        }

        var maxChars = Math.Max(80, (tokenBudget * 4) - 3);
        if (content.Length <= maxChars)
        {
            return content;
        }

        return content[..maxChars].TrimEnd() + "...";
    }

    private static int EstimateTokens(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? 0
            : (int)Math.Ceiling(text.Length / 4d);
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