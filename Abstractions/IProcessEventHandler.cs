using SimpleBPM;

namespace SimpleBPM.Abstractions;

/// <summary>
/// Optional handler for process lifecycle events.
/// Implement this to react to subprocess completion, e.g. for logging,
/// auditing, or triggering side-effects when a subprocess propagates
/// its output back to a parent process.
/// </summary>
public interface IProcessEventHandler
{
    Task OnSubProcessCompletedAsync(SubProcessCompletedEvent evt);
}
