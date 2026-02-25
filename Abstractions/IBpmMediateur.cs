namespace SimpleBPM.Abstractions;

public interface IBpmMediateur
{
    Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
    Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
}
