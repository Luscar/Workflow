using SimpleBPM.Handlers;
using SimpleBPM.Persistence;

namespace SimpleBPM;

public class FlowEngine
{
    private readonly Dictionary<string, List<ProcessDefinition>> _definitions;
    private readonly IProcessRepository _repository;
    private readonly IDefinitionRepository? _definitionRepository;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;

    public FlowEngine(
        IEnumerable<ProcessDefinition> definitions,
        IProcessRepository? repository = null,
        IEnumerable<INodeHandler>? handlers = null,
        IDefinitionRepository? definitionRepository = null)
    {
        _definitions = new Dictionary<string, List<ProcessDefinition>>();
        _repository = repository ?? NullProcessRepository.Instance;
        _definitionRepository = definitionRepository;
        _handlers = new Dictionary<NodeType, INodeHandler>();

        foreach (var def in definitions)
        {
            if (!_definitions.ContainsKey(def.Name))
                _definitions[def.Name] = new List<ProcessDefinition>();
            _definitions[def.Name].Add(def);
        }

        if (handlers != null)
        {
            foreach (var handler in handlers)
                _handlers[handler.NodeType] = handler;
        }

        // Enregistrement automatique des handlers par défaut sans dépendances
        _handlers.TryAdd(NodeType.End, new EndNodeHandler());
        _handlers.TryAdd(NodeType.Interactive, new InteractiveNodeHandler());
        _handlers.TryAdd(NodeType.WaitUntilDate, new WaitUntilDateNodeHandler());
        _handlers.TryAdd(NodeType.WaitForSignal, new WaitForSignalNodeHandler());
        _handlers.TryAdd(NodeType.SubProcess, new SubProcessNodeHandler(_repository, _handlers));
    }

    /// <summary>
    /// Constructeur interne pour les sous-processus qui partagent le registre de handlers du parent.
    /// </summary>
    internal FlowEngine(ProcessDefinition definition, IProcessRepository? repository, Dictionary<NodeType, INodeHandler> handlers)
    {
        _definitions = new Dictionary<string, List<ProcessDefinition>>
        {
            [definition.Name] = new List<ProcessDefinition> { definition }
        };
        _repository = repository ?? NullProcessRepository.Instance;
        _handlers = handlers;
    }

    public async Task<ProcessInstance> ExecuteAsync(ProcessInstance instance)
    {
        await instance.ExecutionLock.WaitAsync();
        try
        {
            return await ExecuteInternalAsync(instance);
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }

    public async Task<ProcessInstance> ContinueAsync(ProcessInstance instance)
    {
        await instance.ExecutionLock.WaitAsync();
        try
        {
            return await ContinueInternalAsync(instance);
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }

    public async Task<ProcessInstance> SignalAsync(ProcessInstance instance, string signalName)
    {
        await instance.ExecutionLock.WaitAsync();
        try
        {
            return await SignalInternalAsync(instance, signalName);
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }

    private async Task<ProcessInstance> ExecuteInternalAsync(ProcessInstance instance)
    {
        var definition = await ResolveDefinitionAsync(instance);
        instance.DefinitionName ??= definition.Name;
        instance.DefinitionVersion ??= definition.Version;
        instance.LastExecutedAt = DateTime.UtcNow;

        var existing = await _repository.GetProcessInstanceAsync(instance.ProcessId);
        if (existing == null)
        {
            await _repository.SaveProcessInstanceAsync(instance);
        }

        var currentNodeId = instance.CurrentNodeId ?? definition.StartNodeId;

        while (!string.IsNullOrEmpty(currentNodeId))
        {
            var node = definition.GetNode(currentNodeId);

            if (node == null)
            {
                instance.Status = ProcessStatus.Failed;
                instance.ErrorMessage = $"Nœud '{currentNodeId}' introuvable dans la définition '{definition.Name}'";
                await _repository.UpdateProcessInstanceAsync(instance);
                return instance;
            }

            if (!_handlers.TryGetValue(node.Type, out var handler))
            {
                instance.Status = ProcessStatus.Failed;
                instance.ErrorMessage = $"Aucun handler enregistré pour le type de nœud '{node.Type}'";
                await _repository.UpdateProcessInstanceAsync(instance);
                return instance;
            }

            // Créer l'entrée d'historique
            var historyEntry = new NodeInstance(node.Name, node.Type);

            var result = await handler.HandleAsync(node, instance);

            // Compléter l'entrée d'historique
            historyEntry.Complete(result.IsCompleted, result.ErrorMessage, result.NextNodeId);
            instance.ExecutionHistory.Add(historyEntry);

            if (!result.IsCompleted)
            {
                instance.Status = ProcessStatus.Failed;
                instance.ErrorMessage = result.ErrorMessage ?? $"Le nœud '{node.DisplayName}' (type : {node.Type}) a échoué";
                await _repository.UpdateProcessInstanceAsync(instance);
                return instance;
            }

            if (result.RequiresStop)
            {
                instance.CurrentNodeId = result.NextNodeId;
                await _repository.UpdateProcessInstanceAsync(instance);
                return instance;
            }

            currentNodeId = result.NextNodeId;
        }

        instance.Status = ProcessStatus.Completed;
        instance.CurrentNodeId = null;
        instance.CompletedAt = DateTime.UtcNow;
        await _repository.UpdateProcessInstanceAsync(instance);
        return instance;
    }

    private async Task<ProcessInstance> ContinueInternalAsync(ProcessInstance instance)
    {
        if (instance.Status == ProcessStatus.Completed || instance.Status == ProcessStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Impossible de continuer le processus '{instance.ProcessId}' : le statut est '{instance.Status}'");
        }

        if (string.IsNullOrEmpty(instance.CurrentNodeId))
        {
            instance.Status = ProcessStatus.Failed;
            instance.ErrorMessage = "Impossible de continuer : aucun nœud courant défini sur l'instance";
            await _repository.UpdateProcessInstanceAsync(instance);
            return instance;
        }

        var definition = await ResolveDefinitionAsync(instance);
        var currentNode = definition.GetNode(instance.CurrentNodeId);

        // Notifier le handler que l'on quitte ce nœud
        if (currentNode != null && _handlers.TryGetValue(currentNode.Type, out var currentHandler))
        {
            await currentHandler.OnLeaveAsync(currentNode, instance);
        }

        instance.Status = ProcessStatus.Running;

        if (currentNode == null || currentNode.NextNodeIds.Count == 0)
        {
            instance.Status = ProcessStatus.Completed;
            instance.CurrentNodeId = null;
            instance.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateProcessInstanceAsync(instance);
            return instance;
        }

        var nextNodeId = currentNode.NextNodeIds.FirstOrDefault();
        instance.CurrentNodeId = nextNodeId;

        return await ExecuteInternalAsync(instance);
    }

    private async Task<ProcessInstance> SignalInternalAsync(ProcessInstance instance, string signalName)
    {
        if (instance.Status != ProcessStatus.WaitingSignal)
        {
            throw new InvalidOperationException(
                $"Impossible d'envoyer un signal au processus '{instance.ProcessId}' : le statut est '{instance.Status}', attendu '{ProcessStatus.WaitingSignal}'");
        }

        if (instance.ExpectedSignal == signalName)
        {
            return await ContinueInternalAsync(instance);
        }

        return instance;
    }

    public async Task<ProcessInstance?> LoadProcessAsync(long processId)
    {
        if (_repository is NullProcessRepository)
        {
            throw new InvalidOperationException("Aucun repository configuré");
        }

        return await _repository.GetProcessInstanceAsync(processId);
    }

    internal ProcessDefinition GetDefinition(string name, string version)
    {
        if (!_definitions.TryGetValue(name, out var versions))
            throw new InvalidOperationException($"Aucune définition trouvée pour '{name}'");

        return versions.FirstOrDefault(d => d.Version == version)
            ?? throw new InvalidOperationException($"Aucune définition trouvée pour '{name}' version '{version}'");
    }

    internal async Task<ProcessDefinition> GetDefinitionAsync(string name, string version)
    {
        if (_definitions.TryGetValue(name, out var versions))
        {
            var cached = versions.FirstOrDefault(d => d.Version == version);
            if (cached != null) return cached;
        }

        if (_definitionRepository != null)
        {
            var def = await _definitionRepository.GetDefinitionAsync(name, version);
            if (def != null)
            {
                CacheDefinition(def);
                return def;
            }
        }

        throw new InvalidOperationException($"Aucune définition trouvée pour '{name}' version '{version}'");
    }

    internal ProcessDefinition GetLatestDefinition(string name)
    {
        if (!_definitions.TryGetValue(name, out var versions) || versions.Count == 0)
            throw new InvalidOperationException($"Aucune définition trouvée pour '{name}'");

        return versions
            .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
            .First();
    }

    internal async Task<ProcessDefinition> GetLatestDefinitionAsync(string name)
    {
        if (_definitions.TryGetValue(name, out var versions) && versions.Count > 0)
        {
            return versions
                .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
                .First();
        }

        if (_definitionRepository != null)
        {
            var dbVersions = await _definitionRepository.GetDefinitionVersionsAsync(name);
            if (dbVersions.Count > 0)
            {
                var latestVersion = dbVersions
                    .OrderByDescending(v => Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0))
                    .First();

                var def = await _definitionRepository.GetDefinitionAsync(name, latestVersion);
                if (def != null)
                {
                    CacheDefinition(def);
                    return def;
                }
            }
        }

        throw new InvalidOperationException($"Aucune définition trouvée pour '{name}'");
    }

    private void CacheDefinition(ProcessDefinition def)
    {
        if (!_definitions.ContainsKey(def.Name))
            _definitions[def.Name] = new List<ProcessDefinition>();

        if (!_definitions[def.Name].Any(d => d.Version == def.Version))
            _definitions[def.Name].Add(def);
    }

    private async Task<ProcessDefinition> ResolveDefinitionAsync(ProcessInstance instance)
    {
        if (!string.IsNullOrEmpty(instance.DefinitionName) && !string.IsNullOrEmpty(instance.DefinitionVersion))
            return await GetDefinitionAsync(instance.DefinitionName, instance.DefinitionVersion);

        if (!string.IsNullOrEmpty(instance.DefinitionName))
            return await GetLatestDefinitionAsync(instance.DefinitionName);

        if (_definitions.Count == 1 && _definitionRepository == null)
            return _definitions.Values.First()
                .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
                .First();

        throw new InvalidOperationException("Impossible de résoudre la définition du processus. Définissez DefinitionName sur l'instance.");
    }
}
