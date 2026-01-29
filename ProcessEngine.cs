using SimpleBPM.Handlers;

namespace SimpleBPM;

public class ProcessEngine
{
    private readonly ProcessDefinition _definition;
    private readonly Persistence.IProcessRepository? _repository;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;

    public ProcessEngine(ProcessDefinition definition, Persistence.IProcessRepository? repository = null, IEnumerable<INodeHandler>? handlers = null)
    {
        _definition = definition;
        _repository = repository;
        _handlers = new Dictionary<NodeType, INodeHandler>();

        if (handlers != null)
        {
            foreach (var handler in handlers)
                _handlers[handler.NodeType] = handler;
        }

        // Auto-register default handlers for nodes without dependencies
        _handlers.TryAdd(NodeType.Interactive, new InteractiveNodeHandler());
        _handlers.TryAdd(NodeType.WaitUntilDate, new WaitUntilDateNodeHandler());
        _handlers.TryAdd(NodeType.WaitForSignal, new WaitForSignalNodeHandler());
        _handlers.TryAdd(NodeType.SubProcess, new SubProcessNodeHandler(repository, _handlers));
    }

    /// <summary>
    /// Internal constructor for sub-processes that share the parent's handler registry.
    /// </summary>
    internal ProcessEngine(ProcessDefinition definition, Persistence.IProcessRepository? repository, Dictionary<NodeType, INodeHandler> handlers)
    {
        _definition = definition;
        _repository = repository;
        _handlers = handlers;
    }

    public async Task<ProcessInstance> ExecuteAsync(ProcessInstance instance)
    {
        instance.DefinitionName ??= _definition.Name;
        instance.DefinitionVersion ??= _definition.Version;
        instance.LastExecutedAt = DateTime.UtcNow;

        if (_repository != null)
        {
            var existing = await _repository.GetProcessInstanceAsync(instance.ProcessId);
            if (existing == null)
            {
                await _repository.SaveProcessInstanceAsync(instance);
            }
        }

        var currentNodeId = instance.CurrentNodeId ?? _definition.StartNodeId;

        while (!string.IsNullOrEmpty(currentNodeId))
        {
            var node = _definition.GetNode(currentNodeId);

            if (node == null)
            {
                instance.Status = ProcessStatus.Failed;
                if (_repository != null)
                {
                    await _repository.UpdateProcessInstanceAsync(instance);
                }
                return instance;
            }

            if (!_handlers.TryGetValue(node.Type, out var handler))
            {
                instance.Status = ProcessStatus.Failed;
                if (_repository != null)
                {
                    await _repository.UpdateProcessInstanceAsync(instance);
                }
                return instance;
            }

            // Créer l'entrée d'historique
            var historyEntry = new NodeExecutionHistory(node.Id, node.Name, node.Type);

            var result = await handler.HandleAsync(node, instance);

            // Compléter l'entrée d'historique
            historyEntry.Complete(result.IsCompleted, result.ErrorMessage, result.NextNodeId);
            instance.ExecutionHistory.Add(historyEntry);

            if (!result.IsCompleted)
            {
                instance.Status = ProcessStatus.Failed;
                if (_repository != null)
                {
                    await _repository.UpdateProcessInstanceAsync(instance);
                }
                return instance;
            }

            if (result.RequiresStop)
            {
                instance.CurrentNodeId = result.NextNodeId;
                if (_repository != null)
                {
                    await _repository.UpdateProcessInstanceAsync(instance);
                }
                return instance;
            }

            currentNodeId = result.NextNodeId;
        }

        instance.Status = ProcessStatus.Completed;
        instance.CurrentNodeId = null;
        instance.CompletedAt = DateTime.UtcNow;
        if (_repository != null)
        {
            await _repository.UpdateProcessInstanceAsync(instance);
        }
        return instance;
    }

    public async Task<ProcessInstance> ContinueAsync(ProcessInstance instance)
    {
        if (instance.Status == ProcessStatus.Completed || instance.Status == ProcessStatus.Failed)
        {
            return instance;
        }

        if (string.IsNullOrEmpty(instance.CurrentNodeId))
        {
            instance.Status = ProcessStatus.Failed;
            if (_repository != null)
            {
                await _repository.UpdateProcessInstanceAsync(instance);
            }
            return instance;
        }

        var currentNode = _definition.GetNode(instance.CurrentNodeId);

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
            if (_repository != null)
            {
                await _repository.UpdateProcessInstanceAsync(instance);
            }
            return instance;
        }

        var nextNodeId = currentNode.NextNodeIds.FirstOrDefault();
        instance.CurrentNodeId = nextNodeId;

        return await ExecuteAsync(instance);
    }

    public async Task<ProcessInstance> SignalAsync(ProcessInstance instance, string signalName)
    {
        if (instance.Status != ProcessStatus.WaitingSignal)
        {
            return instance;
        }

        if (instance.Variables.TryGetValue("WaitingForSignal", out var waitingSignal) &&
            waitingSignal?.ToString() == signalName)
        {
            return await ContinueAsync(instance);
        }

        return instance;
    }

    public async Task<ProcessInstance?> LoadProcessAsync(string processId)
    {
        if (_repository == null)
        {
            throw new InvalidOperationException("No repository configured");
        }

        return await _repository.GetProcessInstanceAsync(processId);
    }
}
