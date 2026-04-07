using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Infrastructure;

/// <summary>
/// Registro de serviços de infraestrutura.
/// Providers mock e infraestrutura em memoria so podem ser usados com opt-in explicito de configuracao.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddCoreInfrastructure();
        services.AddInfrastructureHttpClients();
        services.AddProviderInfrastructure();
        services.AddPersistenceInfrastructure();

        return services;
    }
}
