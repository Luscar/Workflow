using System.Reflection;
using Autofac;
using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Persistence;

namespace SimpleBPM.Localisation;

/// <summary>
/// Module Autofac qui enregistre tous les services SimpleBPM (moteur d'exécution,
/// node handlers, monitoring). Utilise le même builder fluent que l'extension
/// <c>AddSimpleBPM(options => ...)</c> pour la configuration.
/// <example>
/// <code>
/// builder.RegisterModule(new SimpleBPMAutofacModule(module =>
/// {
///     module.ScanHandlers(Assembly.GetExecutingAssembly());
///     module.UseTaskManager&lt;LoanTaskManager&gt;();
///     module.AddProcess(LoanProcessDefinitions.CreateLoanApprovalProcess());
/// }));
/// </code>
/// </example>
/// </summary>
public sealed class SimpleBPMAutofacModule : Module
{
    private readonly List<Assembly> _handlerAssemblies = new();
    private readonly List<ProcessDefinition> _processDefinitions = new();
    private string? _oracleTablePrefix;
    private Type? _taskManagerType;
    private bool _useDefinitionBank;

    public SimpleBPMAutofacModule() { }

    public SimpleBPMAutofacModule(Action<SimpleBPMAutofacModule> configure)
    {
        configure(this);
    }

    /// <summary>
    /// Scans the given assemblies for <see cref="IBpmCommandHandler"/> and
    /// <see cref="IBomQueryHandler"/> implementations and registers them automatically.
    /// </summary>
    public SimpleBPMAutofacModule ScanHandlers(params Assembly[] assemblies)
    {
        _handlerAssemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IGestionTache"/> implementation for task management.
    /// </summary>
    public SimpleBPMAutofacModule UseTaskManager<TManager>() where TManager : class, IGestionTache
    {
        _taskManagerType = typeof(TManager);
        return this;
    }

    /// <summary>
    /// Adds a process definition to the container.
    /// </summary>
    public SimpleBPMAutofacModule AddProcess(ProcessDefinition definition)
    {
        _processDefinitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Configures SimpleBPM to use Oracle persistence with the given table prefix.
    /// When not called, in-memory storage is used.
    /// </summary>
    public SimpleBPMAutofacModule UseOracle(string tablePrefix)
    {
        _oracleTablePrefix = tablePrefix;
        return this;
    }

    /// <summary>
    /// Active la banque de définitions pour sauvegarder et gérer les versions
    /// de <see cref="ProcessDefinition"/> via <see cref="IDefinitionRepository"/>.
    /// Utilise Oracle si <see cref="UseOracle"/> est appelé, sinon stockage en mémoire.
    /// </summary>
    public SimpleBPMAutofacModule UseDefinitionBank()
    {
        _useDefinitionBank = true;
        return this;
    }

    protected override void Load(ContainerBuilder builder)
    {
        // 1. Command/query handler scanning
        if (_handlerAssemblies.Count > 0)
        {
            foreach (var assembly in _handlerAssemblies)
            {
                builder.RegisterAssemblyTypes(assembly)
                    .Where(t => typeof(IBpmCommandHandler).IsAssignableFrom(t))
                    .As<IBpmCommandHandler>()
                    .SingleInstance();

                builder.RegisterAssemblyTypes(assembly)
                    .Where(t => typeof(IBomQueryHandler).IsAssignableFrom(t))
                    .As<IBomQueryHandler>()
                    .SingleInstance();
            }

            builder.RegisterType<BpmMediateur>()
                .As<IBpmMediateur>()
                .IfNotRegistered(typeof(IBpmMediateur))
                .SingleInstance();
        }

        // 2. Task manager
        if (_taskManagerType != null)
        {
            builder.RegisterType(_taskManagerType)
                .As<IGestionTache>()
                .SingleInstance();
        }

        // 3. Process definitions
        foreach (var definition in _processDefinitions)
        {
            builder.RegisterInstance(definition)
                .As<ProcessDefinition>()
                .SingleInstance();
        }

        // 4. Persistence
        if (_oracleTablePrefix is not null)
        {
            builder.Register(_ => new OracleConfiguration(_oracleTablePrefix))
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterType<OracleProcessRepository>()
                .As<IProcessRepository>()
                .InstancePerLifetimeScope();
        }

        builder.RegisterType<InMemoryProcessRepository>()
            .As<IProcessRepository>()
            .IfNotRegistered(typeof(IProcessRepository))
            .SingleInstance();

        // 4b. Banque de définitions
        if (_useDefinitionBank)
        {
            if (_oracleTablePrefix is not null)
            {
                builder.RegisterType<OracleDefinitionRepository>()
                    .As<IDefinitionRepository>()
                    .IfNotRegistered(typeof(IDefinitionRepository))
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<InMemoryDefinitionRepository>()
                    .As<IDefinitionRepository>()
                    .IfNotRegistered(typeof(IDefinitionRepository))
                    .SingleInstance();
            }
        }

        // 5. Node handlers
        builder.RegisterType<BusinessNodeHandler>()
            .As<INodeHandler>()
            .SingleInstance();

        builder.RegisterType<DecisionNodeHandler>()
            .As<INodeHandler>()
            .UsingConstructor(typeof(IBpmMediateur))
            .SingleInstance();

        builder.RegisterType<InteractiveNodeHandler>()
            .As<INodeHandler>()
            .SingleInstance();

        builder.RegisterType<WaitForSignalNodeHandler>()
            .As<INodeHandler>()
            .SingleInstance();

        builder.RegisterType<WaitUntilDateNodeHandler>()
            .As<INodeHandler>()
            .SingleInstance();

        // 6. Core services
        builder.RegisterType<FlowService>()
            .As<IFlowService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ProcessMonitor>()
            .As<IProcessMonitor>()
            .InstancePerLifetimeScope();
    }
}
