using SimpleBPM.Migration;

namespace SimpleBPM;

public interface IFlowService
{
    Task<ProcessStatus> StartAsync(string definitionName, string processId, string? aggregateId = null, Dictionary<string, object>? variables = null);
    Task<ProcessStatus> ContinueAsync(string processId);
    Task<ProcessStatus> SignalAsync(string processId, string signalName);
    Task<ProcessStatus> GetStatusAsync(string processId);
    Task<MigrationResult> MigrateAsync(string processId, ProcessDefinition targetDefinition, ProcessMigration migration);
}
