namespace Chatbot.Infrastructure.Configuration;

public sealed class ProviderExecutionModeOptions
{
    public bool AllowMockProviders { get; set; }
    public bool AllowInMemoryInfrastructure { get; set; }
    public bool PreferMockProviders { get; set; }
    public bool PreferInMemoryInfrastructure { get; set; }
    public bool PreferLocalPersistentInfrastructure { get; set; }
    public bool PreferLocalOcr { get; set; }
}
