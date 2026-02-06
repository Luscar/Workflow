namespace SimpleBPM.Persistence;

public interface IProcessRepository
{
    Task<long> ObtenirSequenceAsync(string nomSequence);
    Task SaveProcessInstanceAsync(ProcessInstance instance);
    Task<ProcessInstance?> GetProcessInstanceAsync(long processId);
    Task UpdateProcessInstanceAsync(ProcessInstance instance);
    Task DeleteProcessInstanceAsync(long processId);
    Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres);
    Task<(NodeExecutionHistory History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId);
    Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeId);
    Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId);
}
