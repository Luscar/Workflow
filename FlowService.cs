using SimpleBPM.Handlers;
using SimpleBPM.Migration;
using SimpleBPM.Persistence;

namespace SimpleBPM;

public class FlowService : IFlowService
{
    private readonly FlowEngine _engine;
    private readonly IProcessRepository _repository;
    private readonly IDefinitionRepository? _definitionRepository;

    public FlowService(IEnumerable<ProcessDefinition> definitions, IProcessRepository repository, IEnumerable<INodeHandler> handlers, IDefinitionRepository? definitionRepository = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _definitionRepository = definitionRepository;
        _engine = new FlowEngine(definitions, repository, handlers, definitionRepository);
    }

    public async Task<Processus> ObtenirAsync(long instanceProcessId)
    {
        var instance = await _repository.GetProcessInstanceAsync(instanceProcessId)
            ?? throw new InvalidOperationException($"Processus '{instanceProcessId}' introuvable");

        return Processus.FromInstance(instance);
    }

    public async Task<long> CreateProcessInstanceAsync(string definitionName, Dictionary<string, object>? variables = null)
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

    public async Task<List<Processus>> RechercherParVariableAsync(List<FiltreVariable> filtres)
    {
        var instances = await _repository.SearchByVariableAsync(filtres);
        return instances.Select(Processus.FromInstance).ToList();
    }

    public async Task<List<Processus>> ObtenirEnfantsAsync(long idInstanceParent)
    {
        var children = await _repository.GetChildrenAsync(idInstanceParent);
        return children.Select(Processus.FromInstance).ToList();
    }

    public async Task<IEnumerable<string>> ObtenirSignauxEnAttenteAsync(long idInstanceProcessus)
    {
        var instance = await _repository.GetProcessInstanceAsync(idInstanceProcessus)
            ?? throw new InvalidOperationException($"Processus '{idInstanceProcessus}' introuvable");

        if (instance.Status == ProcessStatus.WaitingSignal &&
            instance.ExpectedSignal != null)
        {
            return new[] { instance.ExpectedSignal };
        }

        return Enumerable.Empty<string>();
    }

    public async Task<NoeudProcessus> ObtenirNoeudAsync(long idInstanceNoeud)
    {
        var result = await _repository.GetNodeHistoryByIdAsync(idInstanceNoeud)
            ?? throw new InvalidOperationException($"Instance de nœud '{idInstanceNoeud}' introuvable");

        return new NoeudProcessus
        {
            NoSeqNoeud = idInstanceNoeud,
            ProcessId = result.ProcessId,
            NodeId = result.History.NodeId,
            NodeType = result.History.NodeType,
            Success = result.History.Success,
            ErrorMessage = result.History.ErrorMessage,
            StartedAt = result.History.StartedAt,
            CompletedAt = result.History.CompletedAt
        };
    }

    public async Task TerminerEtapeAsync(long idInstanceNoeud, object contenu)
    {
        var nodeResult = await _repository.GetNodeHistoryByIdAsync(idInstanceNoeud)
            ?? throw new InvalidOperationException($"Instance de nœud '{idInstanceNoeud}' introuvable");

        var instance = await _repository.GetProcessInstanceAsync(nodeResult.ProcessId)
            ?? throw new InvalidOperationException($"Processus '{nodeResult.ProcessId}' introuvable");

        if (contenu is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
                instance.Variables[kvp.Key] = kvp.Value;
        }

        await _engine.ContinueAsync(instance);
    }

    public async Task TerminerEtapeEnCoursAsync(long idInstanceProcessus, Dictionary<string, object>? contenu = null)
    {
        var instance = await _repository.GetProcessInstanceAsync(idInstanceProcessus)
            ?? throw new InvalidOperationException($"Processus '{idInstanceProcessus}' introuvable");

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
            ?? throw new InvalidOperationException($"Processus '{idInstanceProcessus}' introuvable");

        await _engine.SignalAsync(instance, signalName);
    }

    public async Task<MigrationResult> MigrateAsync(long processId, ProcessDefinition targetDefinition, ProcessMigration migration)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId)
            ?? throw new InvalidOperationException($"Processus '{processId}' introuvable");

        var sourceDefinition = _engine.GetDefinition(instance.DefinitionName!, instance.DefinitionVersion!);
        var result = ProcessMigrationRunner.Migrate(instance, sourceDefinition, targetDefinition, migration);

        if (result.Success)
        {
            await _repository.UpdateProcessInstanceAsync(instance);
        }

        return result;
    }

    public async Task SauvegarderDefinitionAsync(ProcessDefinition definition)
    {
        if (_definitionRepository == null)
            throw new InvalidOperationException(
                "Aucune banque de définitions configurée. Appelez UseDefinitionBank() lors de l'enregistrement DI.");

        await _definitionRepository.SaveDefinitionAsync(definition);
    }

    public async Task<List<ProcessDefinition>> ObtenirDefinitionsAsync()
    {
        if (_definitionRepository == null)
            throw new InvalidOperationException(
                "Aucune banque de définitions configurée. Appelez UseDefinitionBank() lors de l'enregistrement DI.");

        return await _definitionRepository.GetAllDefinitionsAsync();
    }
}
