using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SimpleBPM.Abstractions;

namespace SimpleBPM.Localisation;

/// <summary>
/// Fluent builder that consolidates all client-side SimpleBPM registrations
/// into a single <c>AddSimpleBPM(options => ...)</c> call.
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
    /// Scans the given assemblies for <see cref="ICommandHandler"/> and
    /// <see cref="IQueryHandler"/> implementations and registers them automatically.
    /// </summary>
    public SimpleBPMBuilder ScanHandlers(params Assembly[] assemblies)
    {
        HandlerAssemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IGestionTache"/> implementation for task management.
    /// </summary>
    public SimpleBPMBuilder UseTaskManager<TManager>() where TManager : class, IGestionTache
    {
        Services.AddSingleton<IGestionTache, TManager>();
        return this;
    }

    /// <summary>
    /// Adds a process definition to the DI container.
    /// </summary>
    public SimpleBPMBuilder AddProcess(ProcessDefinition definition)
    {
        ProcessDefinitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Configures SimpleBPM to use Oracle persistence with the given table prefix.
    /// When not called, in-memory storage is used.
    /// </summary>
    public SimpleBPMBuilder UseOracle(string tablePrefix)
    {
        OracleTablePrefix = tablePrefix;
        return this;
    }
}
