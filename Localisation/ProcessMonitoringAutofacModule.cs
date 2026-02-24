using Autofac;
using SimpleBPM.Persistence;

namespace SimpleBPM.Localisation;

/// <summary>
/// Module Autofac qui enregistre uniquement les services de monitoring en lecture seule
/// (IProcessMonitor) sans le moteur d'exécution ni les node handlers.
/// Utilisé dans les clients de monitoring (ex. Blazor dashboards) qui n'ont
/// pas besoin de IBpmMediator.
/// <example>
/// <code>
/// builder.RegisterModule(new ProcessMonitoringAutofacModule());
/// </code>
/// </example>
/// </summary>
public sealed class ProcessMonitoringAutofacModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<InMemoryProcessRepository>()
            .As<IProcessRepository>()
            .IfNotRegistered(typeof(IProcessRepository))
            .SingleInstance();

        builder.RegisterType<ProcessMonitor>()
            .As<IProcessMonitor>()
            .InstancePerLifetimeScope();
    }
}
