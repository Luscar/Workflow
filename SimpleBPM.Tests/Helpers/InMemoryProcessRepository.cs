using SimpleBPM.Persistence;

namespace SimpleBPM.Tests.Helpers;

public class InMemoryProcessRepository : IProcessRepository
{
    private readonly Dictionary<string, ProcessInstance> _instances = new();

    public Task SaveProcessInstanceAsync(ProcessInstance instance)
    {
        _instances[instance.ProcessId] = instance;
        return Task.CompletedTask;
    }

    public Task<ProcessInstance?> GetProcessInstanceAsync(string processId)
    {
        _instances.TryGetValue(processId, out var instance);
        return Task.FromResult(instance);
    }

    public Task UpdateProcessInstanceAsync(ProcessInstance instance)
    {
        _instances[instance.ProcessId] = instance;
        return Task.CompletedTask;
    }

    public Task DeleteProcessInstanceAsync(string processId)
    {
        _instances.Remove(processId);
        return Task.CompletedTask;
    }

    public Task<List<ProcessInstance>> SearchByVariableAsync(Dictionary<string, object> variablesFiltre)
    {
        var results = _instances.Values
            .Where(i => variablesFiltre.All(f =>
                i.Variables.TryGetValue(f.Key, out var val) && Equals(val, f.Value)))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<(NodeExecutionHistory History, string ProcessId)?> GetNodeHistoryByIdAsync(string historyId)
    {
        foreach (var instance in _instances.Values)
        {
            var history = instance.ExecutionHistory.FirstOrDefault(h => h.NodeId == historyId);
            if (history != null)
                return Task.FromResult<(NodeExecutionHistory History, string ProcessId)?>((history, instance.ProcessId));
        }
        return Task.FromResult<(NodeExecutionHistory History, string ProcessId)?>(null);
    }
}
