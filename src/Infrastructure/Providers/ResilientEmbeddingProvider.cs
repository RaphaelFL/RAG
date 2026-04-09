using Chatbot.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Providers;

public sealed class ResilientEmbeddingProvider : IEmbeddingProvider
{
    private readonly IEmbeddingProvider _primaryProvider;
    private readonly IEmbeddingProvider _fallbackProvider;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<ResilientEmbeddingProvider> _logger;
    private readonly string _primaryProviderName;
    private readonly string _fallbackProviderName;

    public ResilientEmbeddingProvider(
        IEmbeddingProvider primaryProvider,
        IEmbeddingProvider fallbackProvider,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<ResilientEmbeddingProvider> logger,
        string primaryProviderName,
        string fallbackProviderName)
    {
        _primaryProvider = primaryProvider;
        _fallbackProvider = fallbackProvider;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
        _primaryProviderName = primaryProviderName;
        _fallbackProviderName = fallbackProviderName;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        try
        {
            return await _primaryProvider.CreateEmbeddingAsync(text, modelOverride, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no provider primario de embeddings. Aplicando fallback para {FallbackProvider}.", _fallbackProviderName);
            _securityAuditLogger.LogProviderFallback(_primaryProviderName, _fallbackProviderName, ex.Message);
            return await _fallbackProvider.CreateEmbeddingAsync(text, modelOverride, ct);
        }
    }
}