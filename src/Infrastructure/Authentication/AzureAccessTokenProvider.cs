using Azure.Core;
using Azure.Identity;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Authentication;

public interface IAzureAccessTokenProvider
{
    Task<string?> GetTokenAsync(string scope, CancellationToken ct);
}

public sealed class AzureAccessTokenProvider : IAzureAccessTokenProvider
{
    private const string EnableDeviceCodeEnvironmentVariable = "CHATBOT_ENABLE_DEVICE_CODE_AUTH";

    private readonly ExternalProviderClientOptions _options;
    private readonly IReadOnlyList<(string Name, TokenCredential Credential)> _credentials;
    private readonly bool _continueOnDevelopmentCredentialFailures;
    private readonly ILogger<AzureAccessTokenProvider> _logger;

    public AzureAccessTokenProvider(
        IOptions<ExternalProviderClientOptions> options,
        IHostEnvironment environment,
        ILogger<AzureAccessTokenProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _continueOnDevelopmentCredentialFailures = environment.IsDevelopment();
        _credentials = CreateCredentials(environment);
    }

    public async Task<string?> GetTokenAsync(string scope, CancellationToken ct)
    {
        if (!_options.UseAzureAdAuthentication)
        {
            return null;
        }

        var requestContext = new TokenRequestContext([scope]);
        List<string>? failures = null;

        foreach (var entry in _credentials)
        {
            try
            {
                var token = await entry.Credential.GetTokenAsync(requestContext, ct);
                return token.Token;
            }
            catch (CredentialUnavailableException ex)
            {
                failures ??= new List<string>();
                failures.Add($"{entry.Name}: {ex.Message}");
                _logger.LogDebug(ex, "Credencial Azure indisponivel: {credentialName}", entry.Name);
            }
            catch (AuthenticationFailedException ex) when (_continueOnDevelopmentCredentialFailures)
            {
                failures ??= new List<string>();
                failures.Add($"{entry.Name}: {ex.Message}");
                _logger.LogWarning(ex, "Falha ao autenticar com a credencial Azure {credentialName}. Tentando a proxima credencial.", entry.Name);
            }
        }

        throw new AuthenticationFailedException(
            failures is { Count: > 0 }
                ? "Nenhuma credencial Azure conseguiu emitir token. Tentativas: " + string.Join(" | ", failures)
                : "Nenhuma credencial Azure foi configurada para emitir token.");
    }

    private IReadOnlyList<(string Name, TokenCredential Credential)> CreateCredentials(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return new[]
            {
                ("DefaultAzureCredential", (TokenCredential)new DefaultAzureCredential())
            };
        }

        var credentials = new List<(string Name, TokenCredential Credential)>
        {
            ("EnvironmentCredential", new EnvironmentCredential()),
            ("SharedTokenCacheCredential", new SharedTokenCacheCredential()),
            ("VisualStudioCredential", new VisualStudioCredential()),
            ("AzureCliCredential", new AzureCliCredential()),
            ("AzurePowerShellCredential", new AzurePowerShellCredential()),
            ("AzureDeveloperCliCredential", new AzureDeveloperCliCredential())
        };

        if (IsDeviceCodeAuthenticationEnabled())
        {
            credentials.Add(("DeviceCodeCredential", new DeviceCodeCredential(new DeviceCodeCredentialOptions
            {
                DeviceCodeCallback = (deviceCodeInfo, _) =>
                {
                    _logger.LogWarning(
                        "Autenticacao Azure necessaria. Acesse {verificationUri} e informe o codigo {userCode} para continuar.",
                        deviceCodeInfo.VerificationUri,
                        deviceCodeInfo.UserCode);
                    Console.WriteLine($"Autenticacao Azure necessaria. Acesse {deviceCodeInfo.VerificationUri} e informe o codigo {deviceCodeInfo.UserCode}.");
                    return Task.CompletedTask;
                }
            })));
        }

        return credentials;
    }

    private static bool IsDeviceCodeAuthenticationEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnableDeviceCodeEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }
}