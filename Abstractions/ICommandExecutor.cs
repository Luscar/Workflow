namespace SimpleBPM.Abstractions;

public interface ICommandExecutor
{
    Task ExecuteCommandAsync(string commandName, long processId, string? aggregateId);
    Task<string> EvaluateDecisionAsync(string decisionName, long processId, string? aggregateId);
}
