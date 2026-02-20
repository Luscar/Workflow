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

    /// <summary>
    /// Looks up a process by its idempotency key. Returns null if no process exists with that key.
    /// Used by <see cref="IFlowService"/> to deduplicate process creation triggered by the messaging layer.
    /// The default implementation returns null (no deduplication), which is safe for test stubs
    /// and repositories that do not need this feature.
    /// </summary>
    Task<ProcessInstance?> GetByIdempotencyKeyAsync(string idempotencyKey) =>
        Task.FromResult<ProcessInstance?>(null);

    /// <summary>
    /// Returns all process instances. Default implementation delegates to
    /// <see cref="SearchByVariableAsync"/> with an empty filter list.
    /// </summary>
    Task<List<ProcessInstance>> GetAllProcessInstancesAsync() =>
        SearchByVariableAsync(new List<FiltreVariable>());
}
