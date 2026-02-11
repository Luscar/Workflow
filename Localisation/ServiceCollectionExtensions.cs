using System.Reflection;
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
        // Repository en mémoire par défaut si aucun n'est enregistré
        services.TryAddSingleton<IProcessRepository, InMemoryProcessRepository>();

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

        // Monitoring
        services.AddScoped<IProcessMonitor>(sp => new ProcessMonitor(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>()
        ));

        return services;
    }

    /// <summary>
    /// Scans the given assemblies for all <see cref="ICommandHandler"/> and
    /// <see cref="IQueryHandler"/> implementations and registers them in the DI container.
    /// Also registers <see cref="CommandHandlerExecutor"/> as the <see cref="ICommandExecutor"/>,
    /// so the client no longer needs to implement <see cref="ICommandExecutor"/> directly.
    /// </summary>
    public static IServiceCollection AddCommandHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var commandHandlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(ICommandHandler).IsAssignableFrom(t));

            foreach (var type in commandHandlerTypes)
                services.AddSingleton(typeof(ICommandHandler), type);

            var queryHandlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(IQueryHandler).IsAssignableFrom(t));

            foreach (var type in queryHandlerTypes)
                services.AddSingleton(typeof(IQueryHandler), type);
        }

        services.TryAddSingleton<ICommandExecutor, CommandHandlerExecutor>();

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
        // Infrastructure Oracle
        services.AddScoped<OracleConfiguration>(_ => new OracleConfiguration(tablePrefix));
        services.AddScoped<IProcessRepository, OracleProcessRepository>();

        return services.AddSimpleBPM();
    }

    /// <summary>
    /// Point d'entrée unifié qui consolide toute la configuration SimpleBPM
    /// en un seul appel côté client.
    /// <example>
    /// <code>
    /// services.AddSimpleBPM(options =>
    /// {
    ///     options.ScanHandlers(Assembly.GetExecutingAssembly());
    ///     options.UseTaskManager&lt;LoanTaskManager&gt;();
    ///     options.AddProcess(LoanProcessDefinitions.CreateLoanApprovalProcess());
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static IServiceCollection AddSimpleBPM(
        this IServiceCollection services,
        Action<SimpleBPMBuilder> configure)
    {
        var builder = new SimpleBPMBuilder(services);
        configure(builder);

        // 1. Scan and register command/query handlers
        if (builder.HandlerAssemblies.Count > 0)
            services.AddCommandHandlers(builder.HandlerAssemblies.ToArray());

        // 2. Register process definitions
        foreach (var definition in builder.ProcessDefinitions)
            services.AddSingleton(definition);

        // 3. Register infrastructure and core services
        if (builder.OracleTablePrefix is not null)
        {
            services.AddScoped<OracleConfiguration>(_ => new OracleConfiguration(builder.OracleTablePrefix));
            services.AddScoped<IProcessRepository, OracleProcessRepository>();
        }

        return services.AddSimpleBPM();
    }
}
