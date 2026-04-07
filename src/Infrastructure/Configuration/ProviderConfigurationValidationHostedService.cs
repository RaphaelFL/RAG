using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Configuration;

public sealed class ProviderConfigurationValidationHostedService : IHostedService
{
    private readonly ChatModelOptions _chatOptions;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly OcrOptions _ocrOptions;
    private readonly ProviderExecutionModeOptions _executionModeOptions;
    private readonly ExternalProviderClientOptions _providerOptions;

    public ProviderConfigurationValidationHostedService(
        IOptions<ChatModelOptions> chatOptions,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<OcrOptions> ocrOptions,
        IOptions<ProviderExecutionModeOptions> executionModeOptions,
        IOptions<ExternalProviderClientOptions> providerOptions)
    {
        _chatOptions = chatOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _ocrOptions = ocrOptions.Value;
        _executionModeOptions = executionModeOptions.Value;
        _providerOptions = providerOptions.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        ValidateChat(errors);
        ValidateEmbeddings(errors);
        ValidateSearch(errors);
        ValidateBlobStorage(errors);
        ValidateOcr(errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Configuracao invalida do runtime local e de providers opcionais:\n- " + string.Join("\n- ", errors));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateChat(List<string> errors)
    {
        var openAiCompatibleFields = new[]
        {
            _providerOptions.OpenAiCompatibleBaseUrl,
            _providerOptions.OpenAiCompatibleApiKey,
            _providerOptions.OpenAiCompatibleChatModel
        };

        if (HasAnyConfiguredValue(openAiCompatibleFields))
        {
            if (!_providerOptions.HasOpenAiCompatibleChatConfiguration(_chatOptions.Model))
            {
                errors.Add("OpenAI-compatible chat esta parcialmente configurado. Preencha ExternalProviderClientOptions:OpenAiCompatibleBaseUrl e ExternalProviderClientOptions:OpenAiCompatibleChatModel ou ChatModelOptions:Model.");
            }

            return;
        }

        if (!_executionModeOptions.AllowMockProviders)
        {
            errors.Add("Nenhum provider de chat local esta configurado e mocks estao desabilitados. Preencha ExternalProviderClientOptions:OpenAiCompatibleBaseUrl e ExternalProviderClientOptions:OpenAiCompatibleChatModel para o runtime local.");
        }
    }

    private void ValidateEmbeddings(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(_embeddingOptions.Model))
        {
            errors.Add("EmbeddingOptions:Model e obrigatorio para o runtime interno de embeddings.");
        }

        if (_embeddingOptions.Dimensions <= 0)
        {
            errors.Add("EmbeddingOptions:Dimensions deve ser maior que zero para o runtime interno de embeddings.");
        }
    }

    private void ValidateSearch(List<string> errors)
    {
        if (_executionModeOptions.PreferLocalPersistentInfrastructure)
        {
            return;
        }

        if (_executionModeOptions.PreferInMemoryInfrastructure)
        {
            if (!_executionModeOptions.AllowInMemoryInfrastructure)
            {
                errors.Add("Modo local para Search foi solicitado, mas infraestrutura em memoria esta desabilitada.");
            }

            return;
        }

        if (!_executionModeOptions.AllowInMemoryInfrastructure)
        {
            errors.Add("Nenhum backend local de busca foi configurado e infraestrutura em memoria esta desabilitada. Ative PreferLocalPersistentInfrastructure ou AllowInMemoryInfrastructure.");
        }
    }

    private void ValidateBlobStorage(List<string> errors)
    {
        if (_executionModeOptions.PreferLocalPersistentInfrastructure)
        {
            return;
        }

        if (_executionModeOptions.PreferInMemoryInfrastructure)
        {
            if (!_executionModeOptions.AllowInMemoryInfrastructure)
            {
                errors.Add("Modo local para Blob Storage foi solicitado, mas infraestrutura em memoria esta desabilitada.");
            }

            return;
        }

        if (!_executionModeOptions.AllowInMemoryInfrastructure)
        {
            errors.Add("Armazenamento local de documentos nao esta configurado e infraestrutura em memoria esta desabilitada. Ative PreferLocalPersistentInfrastructure ou AllowInMemoryInfrastructure.");
        }
    }

    private void ValidateOcr(List<string> errors)
    {
        if (_executionModeOptions.PreferLocalOcr)
        {
            if (_providerOptions.HasOpenAiCompatibleVisionConfiguration())
            {
                return;
            }

            if (_executionModeOptions.AllowMockProviders)
            {
                return;
            }

            errors.Add("OCR local foi solicitado, mas ExternalProviderClientOptions:OpenAiCompatibleVisionModel nao esta configurado e mocks estao desabilitados.");
            return;
        }

        if (_executionModeOptions.PreferMockProviders)
        {
            if (!_executionModeOptions.AllowMockProviders)
            {
                errors.Add("Modo local para OCR foi solicitado, mas mocks estao desabilitados.");
            }

            return;
        }

        if (!_executionModeOptions.AllowMockProviders)
        {
            errors.Add("Nenhum OCR local esta configurado e mocks estao desabilitados. Preencha ExternalProviderClientOptions:OpenAiCompatibleBaseUrl e ExternalProviderClientOptions:OpenAiCompatibleVisionModel para o runtime local.");
        }
    }

    private static bool HasAnyConfiguredValue(IEnumerable<string?> values)
    {
        return values.Any(ExternalProviderClientOptions.HasConfiguredValue);
    }
}
