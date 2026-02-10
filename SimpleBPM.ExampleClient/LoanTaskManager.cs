using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient;

/// <summary>
/// Manages task lifecycle for interactive workflow nodes.
/// In a real application, this would create/close tasks in a task management system,
/// send notifications, update a UI dashboard, etc.
/// </summary>
public class LoanTaskManager : IGestionTache
{
    public Task CreerTacheAsync(long processId, string? aggregateId, string definitionName, string nodeName)
    {
        Console.WriteLine($"  [TASK] Created task for process {processId}");
        Console.WriteLine($"         Definition: {definitionName}, Node: {nodeName}");
        Console.WriteLine($"         Aggregate: {aggregateId ?? "(none)"}");
        Console.WriteLine($"         -> Waiting for user action...");
        return Task.CompletedTask;
    }

    public Task FermerTacheAsync(long processId, string? aggregateId, string definitionName, string nodeName)
    {
        Console.WriteLine($"  [TASK] Closed task for process {processId}");
        Console.WriteLine($"         Definition: {definitionName}, Node: {nodeName}");
        return Task.CompletedTask;
    }
}
