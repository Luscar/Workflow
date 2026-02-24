namespace SimpleBPM.Abstractions;

public interface IBpmMediator
{
    Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
    Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
}
