using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Chatbot.Infrastructure.Configuration;

public sealed class RuntimeRagRuntimeAdministrationService : IRagRuntimeAdministrationService
{
    private readonly RagRuntimeSettingsStore _store;

    public RuntimeRagRuntimeAdministrationService(RagRuntimeSettingsStore store)
    {
        _store = store;
    }

    public RagRuntimeSettingsDto GetSettings()
    {
        return _store.GetSnapshot();
    }

    public RagRuntimeSettingsDto UpdateSettings(UpdateRagRuntimeSettingsDto request)
    {
        return _store.Update(request);
    }
}