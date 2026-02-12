using Autofac;
using SimpleBPM.Persistence;

namespace SimpleBPM.Localisation;

/// <summary>
/// Module Autofac qui enregistre uniquement les services de monitoring en lecture seule
/// (IProcessMonitor) sans le moteur d'exécution ni les node handlers.
/// Utilisé dans les clients de monitoring (ex. Blazor dashboards) qui n'ont
/// pas besoin de ICommandExecutor.
/// <example>
/// <code>
/// // In-memory (default):
/// builder.RegisterModule(new ProcessMonitoringAutofacModule());
///
/// // With Oracle persistence:
/// builder.RegisterModule(new ProcessMonitoringAutofacModule(m => m.UseOracle("BPM")));
/// </code>
/// </example>
/// </summary>
public sealed class ProcessMonitoringAutofacModule : Module
{
    private string? _oracleTablePrefix;

    public ProcessMonitoringAutofacModule() { }

    public ProcessMonitoringAutofacModule(Action<ProcessMonitoringAutofacModule> configure)
    {
        configure(this);
    }

    /// <summary>
    /// Configures the monitoring module to use Oracle persistence with the given table prefix.
    /// The client must register <see cref="System.Data.IDbConnection"/> separately.
    /// When not called, in-memory storage is used.
    /// </summary>
    public ProcessMonitoringAutofacModule UseOracle(string tablePrefix)
    {
        _oracleTablePrefix = tablePrefix;
        return this;
    }

    protected override void Load(ContainerBuilder builder)
    {
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

        builder.RegisterType<ProcessMonitor>()
            .As<IProcessMonitor>()
            .InstancePerLifetimeScope();
    }
}
