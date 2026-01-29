using SimpleBPM.Handlers;
using SimpleBPM.Migration;
using SimpleBPM.Persistence;

namespace SimpleBPM;

public class FlowService : IFlowService
{
    private readonly Dictionary<string, List<ProcessDefinition>> _definitions = new();
    private readonly IProcessRepository _repository;
    private readonly IEnumerable<INodeHandler> _handlers;

    public FlowService(IEnumerable<ProcessDefinition> definitions, IProcessRepository repository, IEnumerable<INodeHandler> handlers)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));

        foreach (var def in definitions)
        {
            if (!_definitions.ContainsKey(def.Name))
                _definitions[def.Name] = new List<ProcessDefinition>();
            _definitions[def.Name].Add(def);
        }
    }

    public async Task<ProcessStatus> StartAsync(string definitionName, string processId, string? aggregateId = null, Dictionary<string, object>? variables = null)
    {
        var definition = GetLatestDefinition(definitionName);
        var engine = new ProcessEngine(definition, _repository, _handlers);

        var instance = new ProcessInstance(processId, aggregateId);

        if (variables != null)
        {
            foreach (var kvp in variables)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        var result = await engine.ExecuteAsync(instance);
        return result.Status;
    }

    public async Task<ProcessStatus> ContinueAsync(string processId)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        var engine = CreateEngineForInstance(instance);
        var result = await engine.ContinueAsync(instance);
        return result.Status;
    }

    public async Task<ProcessStatus> SignalAsync(string processId, string signalName)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        var engine = CreateEngineForInstance(instance);
        var result = await engine.SignalAsync(instance, signalName);
        return result.Status;
    }

    public async Task<ProcessStatus> GetStatusAsync(string processId)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        return instance.Status;
    }

    public async Task<MigrationResult> MigrateAsync(string processId, ProcessDefinition targetDefinition, ProcessMigration migration)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        var sourceDefinition = GetDefinition(instance.DefinitionName!, instance.DefinitionVersion!);
        var result = ProcessMigrationRunner.Migrate(instance, sourceDefinition, targetDefinition, migration);

        if (result.Success)
        {
            await _repository.UpdateProcessInstanceAsync(instance);
        }

        return result;
    }

    private ProcessEngine CreateEngineForInstance(ProcessInstance instance)
    {
        var definition = GetDefinition(
            instance.DefinitionName ?? throw new InvalidOperationException("Instance has no definition name"),
            instance.DefinitionVersion ?? throw new InvalidOperationException("Instance has no definition version"));

        return new ProcessEngine(definition, _repository, _handlers);
    }

    private ProcessDefinition GetLatestDefinition(string name)
    {
        if (!_definitions.TryGetValue(name, out var versions) || versions.Count == 0)
            throw new InvalidOperationException($"No definition found for '{name}'");

        return versions[^1];
    }

    private ProcessDefinition GetDefinition(string name, string version)
    {
        if (!_definitions.TryGetValue(name, out var versions))
            throw new InvalidOperationException($"No definition found for '{name}'");

        return versions.FirstOrDefault(d => d.Version == version)
            ?? throw new InvalidOperationException($"No definition found for '{name}' version '{version}'");
    }
}
