namespace SimpleBPM.Persistence;

public interface IProcessRepository
{
    Task SaveProcessContextAsync(ProcessContext context);
    Task<ProcessContext?> GetProcessContextAsync(string processId);
    Task UpdateProcessContextAsync(ProcessContext context);
    Task DeleteProcessContextAsync(string processId);
}
