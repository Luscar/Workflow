using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Persistence;

namespace SimpleBPM.Localisation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les services SimpleBPM dans le conteneur DI avec stockage en mémoire.
    /// Le client doit enregistrer <see cref="ICommandExecutor"/> (requis) et
    /// optionnellement <see cref="IGestionTache"/> avant cet appel.
    /// Les <see cref="ProcessDefinition"/> doivent aussi être enregistrées par le client.
    /// </summary>
    public static IServiceCollection AddSimpleBPM(this IServiceCollection services)
    {
        services.TryAddSingleton<IProcessRepository, InMemoryProcessRepository>();

        RegisterHandlers(services);

        services.AddSingleton<IFlowService>(sp => new FlowService(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>(),
            sp.GetServices<INodeHandler>()
        ));

        return services;
    }

    /// <summary>
    /// Enregistre les services SimpleBPM avec persistance Oracle.
    /// Le client doit enregistrer <see cref="ICommandExecutor"/> (requis),
    /// <see cref="System.Data.IDbConnection"/> (requis), et
    /// optionnellement <see cref="IGestionTache"/> avant cet appel.
    /// Les <see cref="ProcessDefinition"/> doivent aussi être enregistrées par le client.
    /// </summary>
    public static IServiceCollection AddSimpleBPM(
        this IServiceCollection services,
        string tablePrefix)
    {
        services.AddScoped<OracleConfiguration>(_ => new OracleConfiguration(tablePrefix));
        services.AddScoped<IProcessRepository, OracleProcessRepository>();

        RegisterHandlers(services);

        services.AddScoped<IFlowService>(sp => new FlowService(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>(),
            sp.GetServices<INodeHandler>()
        ));

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        services.AddSingleton<INodeHandler>(sp =>
            new BusinessNodeHandler(sp.GetRequiredService<ICommandExecutor>()));
        services.AddSingleton<INodeHandler>(sp =>
            new DecisionNodeHandler(sp.GetRequiredService<ICommandExecutor>()));
        services.AddSingleton<INodeHandler>(sp =>
            new InteractiveNodeHandler(sp.GetService<IGestionTache>()));
        services.AddSingleton<INodeHandler, WaitForSignalNodeHandler>();
        services.AddSingleton<INodeHandler, WaitUntilDateNodeHandler>();
    }
}
