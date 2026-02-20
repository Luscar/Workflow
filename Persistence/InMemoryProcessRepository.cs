using System.Collections.Concurrent;

namespace SimpleBPM.Persistence;

public class InMemoryProcessRepository : IProcessRepository
{
    private readonly ConcurrentDictionary<long, ProcessInstance> _processes = new();
    private readonly ConcurrentDictionary<long, (NodeExecutionHistory History, long ProcessId)> _nodeHistories = new();
    private long _sequence;
    private long _historySequence;

    public Task<long> ObtenirSequenceAsync(string nomSequence) =>
        Task.FromResult(Interlocked.Increment(ref _sequence));

    public Task SaveProcessInstanceAsync(ProcessInstance instance)
    {
        _processes[instance.ProcessId] = instance;
        SyncNodeHistories(instance);
        return Task.CompletedTask;
    }

    public Task<ProcessInstance?> GetProcessInstanceAsync(long processId)
    {
        _processes.TryGetValue(processId, out var instance);
        return Task.FromResult(instance);
    }

    public Task UpdateProcessInstanceAsync(ProcessInstance instance)
    {
        _processes[instance.ProcessId] = instance;
        SyncNodeHistories(instance);
        return Task.CompletedTask;
    }

    public Task DeleteProcessInstanceAsync(long processId)
    {
        _processes.TryRemove(processId, out _);
        foreach (var kvp in _nodeHistories.Where(h => h.Value.ProcessId == processId).ToList())
            _nodeHistories.TryRemove(kvp.Key, out _);
        return Task.CompletedTask;
    }

    public Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres)
    {
        var results = _processes.Values
            .Where(p => filtres.All(f =>
                p.Variables.TryGetValue(f.NomVariable, out var val) && f.Correspond(val)))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<(NodeExecutionHistory History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId)
    {
        if (_nodeHistories.TryGetValue(historyId, out var result))
            return Task.FromResult<(NodeExecutionHistory, long)?>(result);
        return Task.FromResult<(NodeExecutionHistory, long)?>(null);
    }

    public Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeId)
    {
        var child = _processes.Values
            .FirstOrDefault(p => p.ParentProcessId == parentProcessId && p.ParentNodeId == parentNodeId);
        return Task.FromResult(child);
    }

    public Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId)
    {
        var children = _processes.Values
            .Where(p => p.ParentProcessId == parentProcessId)
            .ToList();
        return Task.FromResult(children);
    }

    public Task<ProcessInstance?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        var instance = _processes.Values
            .FirstOrDefault(p => p.IdempotencyKey == idempotencyKey);
        return Task.FromResult(instance);
    }

    public Task<List<ProcessInstance>> GetAllProcessInstancesAsync() =>
        Task.FromResult(_processes.Values.ToList());

    private void SyncNodeHistories(ProcessInstance instance)
    {
        var tracked = new HashSet<NodeExecutionHistory>(
            _nodeHistories.Values
                .Where(v => v.ProcessId == instance.ProcessId)
                .Select(v => v.History));

        foreach (var history in instance.ExecutionHistory)
        {
            if (!tracked.Contains(history))
            {
                var id = Interlocked.Increment(ref _historySequence);
                _nodeHistories[id] = (history, instance.ProcessId);
            }
        }
    }
}
