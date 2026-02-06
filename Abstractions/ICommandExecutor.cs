namespace SimpleBPM.Abstractions;

public interface ICommandExecutor
{
    Task ExecuteCommandAsync(string commandName, long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
    Task<string> EvaluateDecisionAsync(string decisionName, long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
}
