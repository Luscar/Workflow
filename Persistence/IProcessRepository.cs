namespace SimpleBPM.Persistence;

public interface IProcessRepository
{
    Task SaveProcessInstanceAsync(ProcessInstance instance);
    Task<ProcessInstance?> GetProcessInstanceAsync(long processId);
    Task UpdateProcessInstanceAsync(ProcessInstance instance);
    Task DeleteProcessInstanceAsync(long processId);
    Task<List<ProcessInstance>> SearchByVariableAsync(Dictionary<string, object> variablesFiltre);
    Task<(NodeExecutionHistory History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId);
}
