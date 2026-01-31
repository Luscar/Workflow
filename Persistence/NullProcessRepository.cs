namespace SimpleBPM.Persistence;

internal sealed class NullProcessRepository : IProcessRepository
{
    public static readonly NullProcessRepository Instance = new();

    private NullProcessRepository() { }

    public Task SaveProcessInstanceAsync(ProcessInstance instance) => Task.CompletedTask;
    public Task<ProcessInstance?> GetProcessInstanceAsync(string processId) => Task.FromResult<ProcessInstance?>(null);
    public Task UpdateProcessInstanceAsync(ProcessInstance instance) => Task.CompletedTask;
    public Task DeleteProcessInstanceAsync(string processId) => Task.CompletedTask;
    public Task<List<ProcessInstance>> SearchByVariableAsync(Dictionary<string, object> variablesFiltre) => Task.FromResult(new List<ProcessInstance>());
    public Task<(NodeExecutionHistory History, string ProcessId)?> GetNodeHistoryByIdAsync(string historyId) => Task.FromResult<(NodeExecutionHistory History, string ProcessId)?>(null);
}
