using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Migration;
using SimpleBPM.Persistence;

namespace SimpleBPM;

public class FlowService : IFlowService
{
    private readonly FlowEngine _engine;
    private readonly IProcessRepository _repository;

    public FlowService(IEnumerable<ProcessDefinition> definitions, IProcessRepository repository, IEnumerable<INodeHandler> handlers, IProcessEventHandler? eventHandler = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _engine = new FlowEngine(definitions, repository, handlers, eventHandler);
    }

    public async Task<Processus> ObtenirAsync(string instanceProcessId)
    {
        var instance = await _repository.GetProcessInstanceAsync(instanceProcessId)
            ?? throw new InvalidOperationException($"Process '{instanceProcessId}' not found");

        return Processus.FromInstance(instance);
    }

    public async Task<string> CreateProcessInstance(string definitionId, Dictionary<string, object>? variables = null)
    {
        var processId = Guid.NewGuid().ToString();
        var instance = new ProcessInstance(processId);
        instance.DefinitionName = definitionId;

        if (variables != null)
        {
            foreach (var kvp in variables)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        await _engine.ExecuteAsync(instance);
        return processId;
    }

    public async Task<List<Processus>> RechercherParVariable(Dictionary<string, object> variablesFiltre)
    {
        var instances = await _repository.SearchByVariableAsync(variablesFiltre);
        return instances.Select(Processus.FromInstance).ToList();
    }

    public async Task<List<Processus>> ObtenirEnfants(string idInstanceParent)
    {
        var parent = await _repository.GetProcessInstanceAsync(idInstanceParent)
            ?? throw new InvalidOperationException($"Process '{idInstanceParent}' not found");

        var children = new List<Processus>();
        foreach (var subProcessId in parent.SubProcessIds.Values)
        {
            var child = await _repository.GetProcessInstanceAsync(subProcessId);
            if (child != null)
                children.Add(Processus.FromInstance(child));
        }
        return children;
    }

    public async Task<Processus?> ObtenirParent(string idInstanceEnfant)
    {
        var child = await _repository.GetProcessInstanceAsync(idInstanceEnfant)
            ?? throw new InvalidOperationException($"Process '{idInstanceEnfant}' not found");

        if (string.IsNullOrEmpty(child.ParentProcessId))
            return null;

        var parent = await _repository.GetProcessInstanceAsync(child.ParentProcessId);
        return parent != null ? Processus.FromInstance(parent) : null;
    }

    public async Task<IEnumerable<string>> ObtenirSignauxEnAttente(string idInstanceProcessus)
    {
        var instance = await _repository.GetProcessInstanceAsync(idInstanceProcessus)
            ?? throw new InvalidOperationException($"Process '{idInstanceProcessus}' not found");

        if (instance.Status == ProcessStatus.WaitingSignal &&
            instance.Variables.TryGetValue("WaitingForSignal", out var signal))
        {
            return new[] { signal?.ToString() ?? string.Empty };
        }

        return Enumerable.Empty<string>();
    }

    public async Task<InstanceNode> Obtenir(string idInstanceNoeud)
    {
        var result = await _repository.GetNodeHistoryByIdAsync(idInstanceNoeud)
            ?? throw new InvalidOperationException($"Node instance '{idInstanceNoeud}' not found");

        return new InstanceNode
        {
            Id = idInstanceNoeud,
            ProcessId = result.ProcessId,
            NodeId = result.History.NodeId,
            NodeName = result.History.NodeName,
            NodeType = result.History.NodeType,
            Success = result.History.Success,
            ErrorMessage = result.History.ErrorMessage,
            StartedAt = result.History.StartedAt,
            CompletedAt = result.History.CompletedAt
        };
    }

    public async Task TerminerEtape(string idInstanceNoeud, object contenu)
    {
        var nodeResult = await _repository.GetNodeHistoryByIdAsync(idInstanceNoeud)
            ?? throw new InvalidOperationException($"Node instance '{idInstanceNoeud}' not found");

        var instance = await _repository.GetProcessInstanceAsync(nodeResult.ProcessId)
            ?? throw new InvalidOperationException($"Process '{nodeResult.ProcessId}' not found");

        if (contenu is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        await _engine.ContinueAsync(instance);
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
