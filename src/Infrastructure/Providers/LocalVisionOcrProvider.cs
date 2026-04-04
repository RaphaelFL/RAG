using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class LocalVisionOcrProvider : IOcrProvider
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalProviderClientOptions _providerOptions;
    private readonly OcrOptions _ocrOptions;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<LocalVisionOcrProvider> _logger;

    public LocalVisionOcrProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ExternalProviderClientOptions> providerOptions,
        IOptions<OcrOptions> ocrOptions,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<LocalVisionOcrProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _providerOptions = providerOptions.Value;
        _ocrOptions = ocrOptions.Value;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_ocrOptions.PrimaryProvider) ? "OllamaVision" : _ocrOptions.PrimaryProvider;

    public async Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var payload = buffer.ToArray();

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var extractedPdfText = PdfTextExtraction.TryExtractText(payload);
            if (!string.IsNullOrWhiteSpace(extractedPdfText))
            {
                return CreateResult(extractedPdfText, "LocalPdfText", 1);
            }

            return CreateFallbackResult(fileName, "PDF escaneado sem renderizador local configurado.");
        }

        if (!SupportedImageExtensions.Contains(extension))
        {
            var extractedText = Encoding.UTF8.GetString(payload);
            return CreateResult(extractedText, ProviderName, 1);
        }

        try
        {
            var extractedText = await ExtractImageTextAsync(payload, fileName, ct);
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                return CreateResult(extractedText, ProviderName, 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no OCR local por vision model para {fileName}.", fileName);
        }

        return CreateFallbackResult(fileName, "OCR vision local nao retornou texto.");
    }

    private async Task<string> ExtractImageTextAsync(byte[] payload, string fileName, CancellationToken ct)
    {
        var mimeType = ResolveMimeType(fileName);
        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(payload)}";
        var client = _httpClientFactory.CreateClient("OpenAICompatible");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

        if (ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.OpenAiCompatibleApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _providerOptions.OpenAiCompatibleApiKey);
        }

        request.Content = JsonContent.Create(new
        {
            model = _providerOptions.OpenAiCompatibleVisionModel,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "Extraia fielmente todo o texto visivel desta imagem. Responda apenas com o texto extraido, sem resumo, sem comentarios e sem markdown."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl
                            }
                        }
                    }
                }
            },
            temperature = 0.0,
            stream = false
        });

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Local vision OCR failed with status {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        return ExtractContent(document.RootElement);
    }

    private OcrResultDto CreateFallbackResult(string fileName, string reason)
    {
        _securityAuditLogger.LogProviderFallback(ProviderName, _ocrOptions.FallbackProvider, reason);
        var fallbackText = $"Conteudo indisponivel para {fileName}";
        return CreateResult(fallbackText, _ocrOptions.FallbackProvider, 1);
    }

    private static OcrResultDto CreateResult(string extractedText, string provider, int pageNumber)
    {
        return new OcrResultDto
        {
            ExtractedText = extractedText,
            Provider = provider,
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = pageNumber,
                    Text = extractedText
                }
            }
        };
    }

    private static string ResolveMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
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