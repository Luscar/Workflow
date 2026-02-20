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

    public async Task<Processus> ObtenirAsync(long instanceProcessId)
    {
        var instance = await _repository.GetProcessInstanceAsync(instanceProcessId)
            ?? throw new InvalidOperationException($"Process '{instanceProcessId}' not found");

        return Processus.FromInstance(instance);
    }

    public async Task<long> CreateProcessInstance(string definitionName, Dictionary<string, object>? variables = null)
    {
        var processId = await _repository.ObtenirSequenceAsync("SEQ_PROCESSUS");
        var instance = new ProcessInstance(processId);
        instance.DefinitionName = definitionName;

        if (variables != null)
        {
            foreach (var kvp in variables)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        await _engine.ExecuteAsync(instance);
        return processId;
    }

    public async Task<long> CreateProcessInstanceIdempotentAsync(string idempotencyKey, string definitionName, Dictionary<string, object>? variables = null)
    {
        // Return early if a process with this key already exists (duplicate message from messaging layer).
        var existing = await _repository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existing != null)
            return existing.ProcessId;

        var processId = await _repository.ObtenirSequenceAsync("SEQ_PROCESSUS");
        var instance = new ProcessInstance(processId)
        {
            DefinitionName = definitionName,
            IdempotencyKey = idempotencyKey
        };

        if (variables != null)
        {
            foreach (var kvp in variables)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        await _engine.ExecuteAsync(instance);
        return processId;
    }

    public async Task<List<Processus>> RechercherParVariable(List<FiltreVariable> filtres)
    {
        var instances = await _repository.SearchByVariableAsync(filtres);
        return instances.Select(Processus.FromInstance).ToList();
    }

    public async Task<List<Processus>> ObtenirEnfants(long idInstanceParent)
    {
        var children = await _repository.GetChildrenAsync(idInstanceParent);
        return children.Select(Processus.FromInstance).ToList();
    }

    public async Task<IEnumerable<string>> ObtenirSignauxEnAttente(long idInstanceProcessus)
    {
        var instance = await _repository.GetProcessInstanceAsync(idInstanceProcessus)
            ?? throw new InvalidOperationException($"Process '{idInstanceProcessus}' not found");

        if (instance.Status == ProcessStatus.WaitingSignal &&
            instance.InternalState.TryGetValue("WaitingForSignal", out var signal))
        {
            return new[] { signal?.ToString() ?? string.Empty };
        }

        return Enumerable.Empty<string>();
    }

    public async Task<InstanceNode> Obtenir(long idInstanceNoeud)
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

    public async Task TerminerEtape(long idInstanceNoeud, object contenu)
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

    public async Task TerminerEtapeEnCours(long idInstanceProcessus, Dictionary<string, object>? contenu = null)
    {
        var instance = await _repository.GetProcessInstanceAsync(idInstanceProcessus)
            ?? throw new InvalidOperationException($"Process '{idInstanceProcessus}' not found");

        if (contenu != null)
        {
            foreach (var kvp in contenu)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        await _engine.ContinueAsync(instance);
    }

    public async Task EnvoyerSignalAsync(long idInstanceProcessus, string signalName)
    {
        var instance = await _repository.GetProcessInstanceAsync(idInstanceProcessus)
            ?? throw new InvalidOperationException($"Process '{idInstanceProcessus}' not found");

        // Idempotency: if the process is no longer waiting for this signal (already processed
        // by a previous delivery), ignore the duplicate instead of throwing.
        if (instance.Status != ProcessStatus.WaitingSignal)
            return;

        await _engine.SignalAsync(instance, signalName);
    }

    public async Task<MigrationResult> MigrateAsync(long processId, ProcessDefinition targetDefinition, ProcessMigration migration)
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
