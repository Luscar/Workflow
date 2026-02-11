using SimpleBPM.Persistence;

namespace SimpleBPM;

public class ProcessMonitor : IProcessMonitor
{
    private readonly IEnumerable<ProcessDefinition> _definitions;
    private readonly IProcessRepository _repository;

    public ProcessMonitor(IEnumerable<ProcessDefinition> definitions, IProcessRepository repository)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public List<ProcessDefinition> GetDefinitions() =>
        _definitions.ToList();

    public async Task<List<Processus>> GetAllInstancesAsync()
    {
        var instances = await _repository.GetAllProcessInstancesAsync();
        return instances.Select(Processus.FromInstance).ToList();
    }

    public async Task<List<Processus>> GetInstancesByStatusAsync(ProcessStatus status)
    {
        var instances = await _repository.GetAllProcessInstancesAsync();
        return instances
            .Where(i => i.Status == status)
            .Select(Processus.FromInstance)
            .ToList();
    }

    public async Task<Processus> GetInstanceAsync(long processId)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        return Processus.FromInstance(instance);
    }

    public async Task<List<NodeExecutionHistory>> GetExecutionHistoryAsync(long processId)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        return instance.ExecutionHistory;
    }

    public async Task<Dictionary<ProcessStatus, int>> GetStatusSummaryAsync()
    {
        var instances = await _repository.GetAllProcessInstancesAsync();

        var summary = new Dictionary<ProcessStatus, int>();
        foreach (ProcessStatus status in Enum.GetValues<ProcessStatus>())
        {
            summary[status] = instances.Count(i => i.Status == status);
        }

        return summary;
    }
}
