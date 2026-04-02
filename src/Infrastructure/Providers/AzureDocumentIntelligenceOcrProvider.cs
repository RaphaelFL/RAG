using System.Net.Http.Json;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Authentication;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class AzureDocumentIntelligenceOcrProvider : IOcrProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OcrOptions _ocrOptions;
    private readonly ExternalProviderClientOptions _providerOptions;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly IAzureAccessTokenProvider _azureAccessTokenProvider;

    public AzureDocumentIntelligenceOcrProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<OcrOptions> ocrOptions,
        IOptions<ExternalProviderClientOptions> providerOptions,
        ISecurityAuditLogger securityAuditLogger,
        IAzureAccessTokenProvider azureAccessTokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _ocrOptions = ocrOptions.Value;
        _providerOptions = providerOptions.Value;
        _securityAuditLogger = securityAuditLogger;
        _azureAccessTokenProvider = azureAccessTokenProvider;
    }

    public string ProviderName => _ocrOptions.PrimaryProvider;

    public async Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct)
    {
        var azureResult = await TryExtractWithAzureDocumentIntelligenceAsync(content, ct);
        if (!string.IsNullOrWhiteSpace(azureResult.ExtractedText))
        {
            azureResult.Provider = _ocrOptions.PrimaryProvider;
            return azureResult;
        }

        if (_ocrOptions.EnableFallback && _providerOptions.HasGoogleVisionConfiguration())
        {
            _securityAuditLogger.LogProviderFallback(_ocrOptions.PrimaryProvider, _ocrOptions.FallbackProvider, "Empty OCR extraction result.");
            var googleResult = await TryExtractWithGoogleVisionAsync(content, fileName, ct);
            googleResult.Provider = _ocrOptions.FallbackProvider;
            return googleResult;
        }

        azureResult.Provider = _ocrOptions.PrimaryProvider;
        return azureResult;
    }

    private async Task<OcrResultDto> TryExtractWithAzureDocumentIntelligenceAsync(Stream content, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AzureDocumentIntelligence");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/documentintelligence/documentModels/{Uri.EscapeDataString(_ocrOptions.AzureDocumentIntelligenceModelId)}:analyze?api-version={Uri.EscapeDataString(_providerOptions.AzureDocumentIntelligenceApiVersion)}&outputContentFormat=text");
        await ApplyDocumentIntelligenceAuthenticationAsync(request, ct);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        using var submitResponse = await client.SendAsync(request, ct);
        var submitPayload = await submitResponse.Content.ReadAsStringAsync(ct);
        if (!submitResponse.IsSuccessStatusCode || !submitResponse.Headers.TryGetValues("operation-location", out var values))
        {
            throw new InvalidOperationException($"Azure Document Intelligence OCR failed with status {(int)submitResponse.StatusCode}: {submitPayload}");
        }

        var operationLocation = values.First();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500, ct);
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            await ApplyDocumentIntelligenceAuthenticationAsync(statusRequest, ct);
            using var statusResponse = await client.SendAsync(statusRequest, ct);
            var statusPayload = await statusResponse.Content.ReadAsStringAsync(ct);
            if (!statusResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Azure Document Intelligence OCR polling failed with status {(int)statusResponse.StatusCode}: {statusPayload}");
            }

            using var document = JsonDocument.Parse(statusPayload);
            var root = document.RootElement;
            var status = root.GetProperty("status").GetString();
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                var analyzeResult = root.GetProperty("analyzeResult");
                var extractedText = analyzeResult.TryGetProperty("content", out var contentElement)
                    ? contentElement.GetString() ?? string.Empty
                    : string.Empty;

                return new OcrResultDto
                {
                    ExtractedText = extractedText,
                    Pages = new List<PageExtractionDto>
                    {
                        new()
                        {
                            PageNumber = 1,
                            Text = extractedText
                        }
                    }
                };
            }

            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Azure Document Intelligence OCR failed: {statusPayload}");
            }
        }

        throw new TimeoutException("Azure Document Intelligence OCR polling timed out.");
    }

    private async Task<OcrResultDto> TryExtractWithGoogleVisionAsync(Stream content, string fileName, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var payload = Convert.ToBase64String(ms.ToArray());
        var client = _httpClientFactory.CreateClient("GoogleVision");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/images:annotate?key={Uri.EscapeDataString(_providerOptions.GoogleVisionApiKey)}");
        request.Content = JsonContent.Create(new
        {
            requests = new[]
            {
                new
                {
                    image = new { content = payload },
                    features = new[] { new { type = "DOCUMENT_TEXT_DETECTION" } },
                    imageContext = new { languageHints = new[] { "pt", "en" } }
                }
            }
        });

        using var response = await client.SendAsync(request, ct);
        var responsePayload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Vision OCR failed with status {(int)response.StatusCode}: {responsePayload}");
        }

        using var document = JsonDocument.Parse(responsePayload);
        var root = document.RootElement;
        var extractedText = root.GetProperty("responses")[0].TryGetProperty("fullTextAnnotation", out var fullTextAnnotation)
            && fullTextAnnotation.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : $"No text extracted from {fileName}";

        return new OcrResultDto
        {
            ExtractedText = extractedText,
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = extractedText
                }
            }
        };
    }

    private async Task ApplyDocumentIntelligenceAuthenticationAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureDocumentIntelligenceApiKey))
        {
            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _providerOptions.AzureDocumentIntelligenceApiKey);
            return;
        }

        var token = await _azureAccessTokenProvider.GetTokenAsync("https://cognitiveservices.azure.com/.default", ct);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}