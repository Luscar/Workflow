using SimpleBPM.Handlers;
using SimpleBPM.Migration;
using SimpleBPM.Persistence;

namespace SimpleBPM;

public class FlowService : IFlowService
{
    private readonly FlowEngine _engine;
    private readonly IProcessRepository _repository;

    public FlowService(IEnumerable<ProcessDefinition> definitions, IProcessRepository repository, IEnumerable<INodeHandler> handlers)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _engine = new FlowEngine(definitions, repository, handlers);
    }

    public async Task<ProcessStatus> StartAsync(string definitionName, string processId, string? aggregateId = null, Dictionary<string, object>? variables = null)
    {
        var instance = new ProcessInstance(processId, aggregateId);
        instance.DefinitionName = definitionName;

        if (variables != null)
        {
            foreach (var kvp in variables)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        var result = await _engine.ExecuteAsync(instance);
        return result.Status;
    }

    public async Task<ProcessStatus> ContinueAsync(string processId)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        var result = await _engine.ContinueAsync(instance);
        return result.Status;
    }

    public async Task<ProcessStatus> SignalAsync(string processId, string signalName)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Process '{processId}' not found");

        var result = await _engine.SignalAsync(instance, signalName);
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

        var sourceDefinition = _engine.GetDefinition(instance.DefinitionName!, instance.DefinitionVersion!);
        var result = ProcessMigrationRunner.Migrate(instance, sourceDefinition, targetDefinition, migration);

        if (result.Success)
        {
            await _repository.UpdateProcessInstanceAsync(instance);
        }

        return result;
    }
}
