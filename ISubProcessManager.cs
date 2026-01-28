namespace SimpleBPM;

public interface ISubProcessManager
{
    Task SaveSubProcessContextAsync(string parentProcessId, string subProcessId, ProcessContext subContext);
    Task<ProcessContext?> GetSubProcessContextAsync(string parentProcessId, string subProcessId);
    Task DeleteSubProcessContextAsync(string parentProcessId, string subProcessId);
}
