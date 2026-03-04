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
    /// Le client doit enregistrer <see cref="IBpmMediateur"/> (requis) et
    /// optionnellement <see cref="IGestionTache"/> avant cet appel.
    /// Les <see cref="ProcessDefinition"/> doivent aussi être enregistrées par le client.
    /// </summary>
    public static IServiceCollection AddSimpleBPM(this IServiceCollection services)
    {
        // Repositories en mémoire par défaut si aucun n'est enregistré
        services.TryAddSingleton<IProcessRepository, InMemoryProcessRepository>();
        services.TryAddSingleton<IDefinitionRepository, InMemoryDefinitionRepository>();

        // Node handlers (sauf SubProcessNodeHandler qui est auto-enregistré par le moteur)
        services.AddSingleton<INodeHandler>(sp =>
            new BusinessNodeHandler(sp.GetRequiredService<IBpmMediateur>()));
        services.AddSingleton<INodeHandler>(sp =>
            new DecisionNodeHandler(sp.GetRequiredService<IBpmMediateur>()));
        services.AddSingleton<INodeHandler>(sp =>
            new InteractiveNodeHandler(sp.GetService<IGestionTache>()));
        services.AddSingleton<INodeHandler, WaitForSignalNodeHandler>();
        services.AddSingleton<INodeHandler, WaitUntilDateNodeHandler>();

        // Service principal
        services.AddScoped<IFlowService>(sp => new FlowService(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>(),
            sp.GetServices<INodeHandler>(),
            sp.GetService<IDefinitionRepository>()
        ));

        // Monitoring
        services.AddScoped<IProcessMonitor>(sp => new ProcessMonitor(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>(),
            sp.GetService<IDefinitionRepository>()
        ));

        return services;
    }

    /// <summary>
    /// Enregistre uniquement les services de surveillance en lecture seule (IProcessMonitor) sans
    /// le moteur d'exécution ni les handlers de nœuds. À utiliser dans les clients de surveillance uniquement
    /// (ex. tableaux de bord Blazor) qui n'ont pas besoin de IBpmMediateur.
    /// </summary>
    public static IServiceCollection AddProcessMonitoring(this IServiceCollection services)
    {
        services.TryAddSingleton<IProcessRepository, InMemoryProcessRepository>();
        services.TryAddSingleton<IDefinitionRepository, InMemoryDefinitionRepository>();

        services.AddScoped<IProcessMonitor>(sp => new ProcessMonitor(
            sp.GetServices<ProcessDefinition>(),
            sp.GetRequiredService<IProcessRepository>(),
            sp.GetService<IDefinitionRepository>()
        ));

        return services;
    }

    /// <summary>
    /// Scanne les assemblies données pour toutes les implémentations de <see cref="IBpmCommandHandler"/> et
    /// <see cref="IBpmQueryHandler"/> et les enregistre dans le conteneur DI.
    /// Enregistre également <see cref="BpmMediateur"/> comme <see cref="IBpmMediateur"/>,
    /// de sorte que le client n'a plus besoin d'implémenter <see cref="IBpmMediateur"/> directement.
    /// </summary>
    public static IServiceCollection AddCommandHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var commandHandlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(IBpmCommandHandler).IsAssignableFrom(t));

            foreach (var type in commandHandlerTypes)
                services.AddSingleton(typeof(IBpmCommandHandler), type);

            var queryHandlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(IBpmQueryHandler).IsAssignableFrom(t));

            foreach (var type in queryHandlerTypes)
                services.AddSingleton(typeof(IBpmQueryHandler), type);
        }

        services.TryAddSingleton<IBpmMediateur, BpmMediateur>();

        return services;
    }

    /// <summary>
    /// Enregistre les services SimpleBPM avec persistance Oracle.
    /// Le client doit enregistrer <see cref="IBpmMediateur"/> (requis),
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
        services.AddScoped<IDefinitionRepository, OracleDefinitionRepository>();

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

        // 1. Scanner et enregistrer les handlers de commandes/requêtes
        if (builder.HandlerAssemblies.Count > 0)
            services.AddCommandHandlers(builder.HandlerAssemblies.ToArray());

        // 2. Enregistrer les définitions de processus
        foreach (var definition in builder.ProcessDefinitions)
            services.AddSingleton(definition);

        // 3. Enregistrer l'infrastructure et les services principaux
        if (builder.OracleTablePrefix is not null)
        {
            services.AddScoped<OracleConfiguration>(_ => new OracleConfiguration(builder.OracleTablePrefix));
            services.AddScoped<IProcessRepository, OracleProcessRepository>();
            services.AddScoped<IDefinitionRepository, OracleDefinitionRepository>();
        }

        return services.AddSimpleBPM();
    }
}
