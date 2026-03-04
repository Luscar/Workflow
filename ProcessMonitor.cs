using SimpleBPM.Persistence;

namespace SimpleBPM;

public class ProcessMonitor : IProcessMonitor
{
    private readonly List<ProcessDefinition> _definitions;
    private readonly IProcessRepository _repository;
    private readonly IDefinitionRepository? _definitionRepository;

    public ProcessMonitor(
        IEnumerable<ProcessDefinition> definitions,
        IProcessRepository repository,
        IDefinitionRepository? definitionRepository = null)
    {
        _definitions = (definitions ?? throw new ArgumentNullException(nameof(definitions))).ToList();
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _definitionRepository = definitionRepository;
    }

    public List<ProcessDefinition> GetDefinitions() =>
        _definitions.ToList();

    public async Task<List<ProcessDefinition>> GetDefinitionsAsync()
    {
        var definitions = new Dictionary<(string Name, string Version), ProcessDefinition>();

        if (_definitionRepository != null)
        {
            var dbDefs = await _definitionRepository.GetAllDefinitionsAsync();
            foreach (var def in dbDefs)
                definitions[(def.Name, def.Version)] = def;
        }

        foreach (var def in _definitions)
            definitions[(def.Name, def.Version)] = def;

        return definitions.Values.ToList();
    }

    public async Task<List<Processus>> GetAllInstancesAsync()
    {
        var instances = await _repository.GetAllProcessInstancesAsync();
        return instances.Select(Processus.FromInstance).ToList();
    }

    public async Task<List<Processus>> GetRootInstancesAsync()
    {
        var instances = await _repository.GetAllProcessInstancesAsync();
        return instances
            .Where(i => i.ParentProcessId == null)
            .Select(Processus.FromInstance)
            .ToList();
    }

    public async Task<List<Processus>> GetAllDescendantsAsync(long processId)
    {
        var all = await _repository.GetAllProcessInstancesAsync();
        var result = new List<Processus>();
        CollectDescendants(processId, all, result);
        return result;
    }

    private static void CollectDescendants(long parentId, List<ProcessInstance> all, List<Processus> result)
    {
        foreach (var child in all.Where(i => i.ParentProcessId == parentId))
        {
            result.Add(Processus.FromInstance(child));
            CollectDescendants(child.ProcessId, all, result);
        }
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

    public async Task<List<NodeInstance>> GetExecutionHistoryAsync(long processId)
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
