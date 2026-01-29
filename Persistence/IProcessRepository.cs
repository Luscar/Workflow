namespace SimpleBPM.Persistence;

public interface IProcessRepository
{
    Task SaveProcessInstanceAsync(ProcessInstance instance);
    Task<ProcessInstance?> GetProcessInstanceAsync(string processId);
    Task UpdateProcessInstanceAsync(ProcessInstance instance);
    Task DeleteProcessInstanceAsync(string processId);
}
