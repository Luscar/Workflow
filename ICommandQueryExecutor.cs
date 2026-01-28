namespace SimpleBPM;

public interface ICommandQueryExecutor
{
    Task ExecuteAsync(string commandOrQueryName, string processId, string? aggregateId, bool isQuery);
    Task<string> ExecuteDecisionAsync(string queryName, string processId, string? aggregateId);
}
