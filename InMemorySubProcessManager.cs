namespace SimpleBPM;

public class InMemorySubProcessManager : ISubProcessManager
{
    private readonly Dictionary<string, ProcessContext> _subProcesses = new();

    public Task SaveSubProcessContextAsync(string parentProcessId, string subProcessId, ProcessContext subContext)
    {
        var key = $"{parentProcessId}:{subProcessId}";
        _subProcesses[key] = subContext;
        return Task.CompletedTask;
    }

    public Task<ProcessContext?> GetSubProcessContextAsync(string parentProcessId, string subProcessId)
    {
        var key = $"{parentProcessId}:{subProcessId}";
        _subProcesses.TryGetValue(key, out var context);
        return Task.FromResult(context);
    }

    public Task DeleteSubProcessContextAsync(string parentProcessId, string subProcessId)
    {
        var key = $"{parentProcessId}:{subProcessId}";
        _subProcesses.Remove(key);
        return Task.CompletedTask;
    }
}
