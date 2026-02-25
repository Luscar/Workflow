using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SimpleBPM.Abstractions;

namespace SimpleBPM.Localisation;

/// <summary>
/// Constructeur fluide qui consolide toutes les configurations SimpleBPM côté client
/// en un seul appel <c>AddSimpleBPM(options => ...)</c>.
/// </summary>
public sealed class SimpleBPMBuilder
{
    internal readonly IServiceCollection Services;
    internal readonly List<Assembly> HandlerAssemblies = new();
    internal readonly List<ProcessDefinition> ProcessDefinitions = new();
    internal string? OracleTablePrefix;

    internal SimpleBPMBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Scanne les assemblies données pour les implémentations de <see cref="ICommandHandler"/> et
    /// <see cref="IQueryHandler"/> et les enregistre automatiquement.
    /// </summary>
    public SimpleBPMBuilder ScanHandlers(params Assembly[] assemblies)
    {
        HandlerAssemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Enregistre une implémentation personnalisée de <see cref="IGestionTache"/> pour la gestion des tâches.
    /// </summary>
    public SimpleBPMBuilder UseTaskManager<TManager>() where TManager : class, IGestionTache
    {
        Services.AddSingleton<IGestionTache, TManager>();
        return this;
    }

    /// <summary>
    /// Ajoute une définition de processus dans le conteneur DI.
    /// </summary>
    public SimpleBPMBuilder AddProcess(ProcessDefinition definition)
    {
        ProcessDefinitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Configure SimpleBPM pour utiliser la persistance Oracle avec le préfixe de table donné.
    /// Si non appelé, le stockage en mémoire est utilisé.
    /// </summary>
    public SimpleBPMBuilder UseOracle(string tablePrefix)
    {
        OracleTablePrefix = tablePrefix;
        return this;
    }
}
