namespace SimpleBPM.Abstractions;

public interface ICommandExecutor
{
    Task ExecuteCommandAsync(string commandName, string processId, string? aggregateId);
    Task<string> EvaluateDecisionAsync(string decisionName, string processId, string? aggregateId);
}
