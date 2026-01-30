namespace SimpleBPM.Persistence;

public interface IProcessRepository
{
    Task SaveProcessInstanceAsync(ProcessInstance instance);
    Task<ProcessInstance?> GetProcessInstanceAsync(string processId);
    Task UpdateProcessInstanceAsync(ProcessInstance instance);
    Task DeleteProcessInstanceAsync(string processId);
    Task<List<ProcessInstance>> SearchByVariableAsync(Dictionary<string, object> variablesFiltre);
    Task<(NodeExecutionHistory History, string ProcessId)?> GetNodeHistoryByIdAsync(string historyId);
}
