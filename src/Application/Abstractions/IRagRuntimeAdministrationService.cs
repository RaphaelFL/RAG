namespace Chatbot.Application.Abstractions;

public interface IRagRuntimeAdministrationService
{
    RagRuntimeSettingsDto GetSettings();
    RagRuntimeSettingsDto UpdateSettings(UpdateRagRuntimeSettingsDto request);
}