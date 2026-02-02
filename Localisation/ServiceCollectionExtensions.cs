using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Persistence;

namespace SimpleBPM.Localisation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les services SimpleBPM dans le conteneur DI.
    /// Le client doit enregistrer <see cref="ICommandExecutor"/> (requis) et
    /// optionnellement <see cref="IGestionTache"/> avant cet appel.
    /// Les <see cref="ProcessDefinition"/> doivent aussi être enregistrées par le client.
    /// </summary>
    public static IServiceCollection AddSimpleBPM(
        this IServiceCollection services,
        string connectionString,
        string tablePrefix)
    {
        // Infrastructure Oracle
        services.AddScoped<OracleConfiguration>(_ => new OracleConfiguration(connectionString, tablePrefix));
        services.AddScoped<IDbConnection>(sp =>
        {
            var config = sp.GetRequiredService<OracleConfiguration>();
            var conn = new OracleConnection(config.ConnectionString);
            conn.Open();
            return conn;
        });
        services.AddScoped<IProcessRepository, OracleProcessRepository>();

        // Node handlers (sauf SubProcessNodeHandler qui est auto-enregistré par le moteur)
        services.AddSingleton<INodeHandler>(sp =>
            new BusinessNodeHandler(sp.GetRequiredService<ICommandExecutor>()));
        services.AddSingleton<INodeHandler>(sp =>
            new DecisionNodeHandler(sp.GetRequiredService<ICommandExecutor>()));
        services.AddSingleton<INodeHandler>(sp =>
            new InteractiveNodeHandler(sp.GetService<IGestionTache>()));
        services.AddSingleton<INodeHandler, WaitForSignalNodeHandler>();
        services.AddSingleton<INodeHandler, WaitUntilDateNodeHandler>();

        // Service principal
        services.AddScoped<IFlowService>(sp => new FlowService(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>(),
            sp.GetServices<INodeHandler>()
        ));

        return services;
    }
}
