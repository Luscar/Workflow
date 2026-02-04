namespace SimpleBPM.Persistence;

internal sealed class NullProcessRepository : IProcessRepository
{
    public static readonly NullProcessRepository Instance = new();

    private NullProcessRepository() { }

    public Task SaveProcessInstanceAsync(ProcessInstance instance) => Task.CompletedTask;
    public Task<ProcessInstance?> GetProcessInstanceAsync(long processId) => Task.FromResult<ProcessInstance?>(null);
    public Task UpdateProcessInstanceAsync(ProcessInstance instance) => Task.CompletedTask;
    public Task DeleteProcessInstanceAsync(long processId) => Task.CompletedTask;
    public Task<List<ProcessInstance>> SearchByVariableAsync(Dictionary<string, object> variablesFiltre) => Task.FromResult(new List<ProcessInstance>());
    public Task<(NodeExecutionHistory History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId) => Task.FromResult<(NodeExecutionHistory History, long ProcessId)?>(null);
}
