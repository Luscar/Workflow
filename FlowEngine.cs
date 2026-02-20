using SimpleBPM.Handlers;
using SimpleBPM.Persistence;

namespace SimpleBPM;

public class FlowEngine
{
    private readonly Dictionary<string, List<ProcessDefinition>> _definitions;
    private readonly IProcessRepository _repository;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;

    public FlowEngine(IEnumerable<ProcessDefinition> definitions, IProcessRepository? repository = null, IEnumerable<INodeHandler>? handlers = null)
    {
        _definitions = new Dictionary<string, List<ProcessDefinition>>();
        _repository = repository ?? NullProcessRepository.Instance;
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

        // Auto-register default handlers for nodes without dependencies
        _handlers.TryAdd(NodeType.End, new EndNodeHandler());
        _handlers.TryAdd(NodeType.Interactive, new InteractiveNodeHandler());
        _handlers.TryAdd(NodeType.WaitUntilDate, new WaitUntilDateNodeHandler());
        _handlers.TryAdd(NodeType.WaitForSignal, new WaitForSignalNodeHandler());
        _handlers.TryAdd(NodeType.SubProcess, new SubProcessNodeHandler(_repository, _handlers));
    }

    /// <summary>
    /// Internal constructor for sub-processes that share the parent's handler registry.
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
        var definition = ResolveDefinition(instance);
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
                instance.ErrorMessage = $"Node '{currentNodeId}' not found in definition '{definition.Name}'";
                await _repository.UpdateProcessInstanceAsync(instance);
                return instance;
            }

            if (!_handlers.TryGetValue(node.Type, out var handler))
            {
                instance.Status = ProcessStatus.Failed;
                instance.ErrorMessage = $"No handler registered for node type '{node.Type}'";
                await _repository.UpdateProcessInstanceAsync(instance);
                return instance;
            }

            // Idempotency check: if this node already completed successfully (e.g. engine restart
            // after a crash mid-run, or a duplicate message from the messaging layer), skip
            // re-execution and resume from the recorded next node instead.
            var previousSuccess = instance.ExecutionHistory
                .LastOrDefault(h => h.NodeId == node.Name && h.Success);
            if (previousSuccess != null)
            {
                currentNodeId = previousSuccess.NextNodeId;
                continue;
            }

            // Créer l'entrée d'historique
            var historyEntry = new NodeExecutionHistory(node.Name, node.DisplayName, node.Type);

            var result = await handler.HandleAsync(node, instance);

            // Compléter l'entrée d'historique
            historyEntry.Complete(result.IsCompleted, result.ErrorMessage, result.NextNodeId);
            instance.ExecutionHistory.Add(historyEntry);

            if (!result.IsCompleted)
            {
                instance.Status = ProcessStatus.Failed;
                instance.ErrorMessage = result.ErrorMessage ?? $"Node '{node.DisplayName}' (type: {node.Type}) failed";
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
                $"Cannot continue process '{instance.ProcessId}': status is '{instance.Status}'");
        }

        if (string.IsNullOrEmpty(instance.CurrentNodeId))
        {
            instance.Status = ProcessStatus.Failed;
            instance.ErrorMessage = "Cannot continue: no current node set on the instance";
            await _repository.UpdateProcessInstanceAsync(instance);
            return instance;
        }

        var definition = ResolveDefinition(instance);
        var currentNode = definition.GetNode(instance.CurrentNodeId);

        // Notify handler that we are leaving this node
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
                $"Cannot signal process '{instance.ProcessId}': status is '{instance.Status}', expected '{ProcessStatus.WaitingSignal}'");
        }

        if (instance.InternalState.TryGetValue("WaitingForSignal", out var waitingSignal) &&
            waitingSignal?.ToString() == signalName)
        {
            return await ContinueInternalAsync(instance);
        }

        return instance;
    }

    public async Task<ProcessInstance?> LoadProcessAsync(long processId)
    {
        if (_repository is NullProcessRepository)
        {
            throw new InvalidOperationException("No repository configured");
        }

        return await _repository.GetProcessInstanceAsync(processId);
    }

    internal ProcessDefinition GetDefinition(string name, string version)
    {
        if (!_definitions.TryGetValue(name, out var versions))
            throw new InvalidOperationException($"No definition found for '{name}'");

        return versions.FirstOrDefault(d => d.Version == version)
            ?? throw new InvalidOperationException($"No definition found for '{name}' version '{version}'");
    }

    internal ProcessDefinition GetLatestDefinition(string name)
    {
        if (!_definitions.TryGetValue(name, out var versions) || versions.Count == 0)
            throw new InvalidOperationException($"No definition found for '{name}'");

        return versions
            .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
            .First();
    }

    private ProcessDefinition ResolveDefinition(ProcessInstance instance)
    {
        if (!string.IsNullOrEmpty(instance.DefinitionName) && !string.IsNullOrEmpty(instance.DefinitionVersion))
            return GetDefinition(instance.DefinitionName, instance.DefinitionVersion);

        if (!string.IsNullOrEmpty(instance.DefinitionName))
            return GetLatestDefinition(instance.DefinitionName);

        if (_definitions.Count == 1)
            return _definitions.Values.First()
                .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
                .First();

        throw new InvalidOperationException("Cannot resolve process definition. Set DefinitionName on the instance.");
    }
}
