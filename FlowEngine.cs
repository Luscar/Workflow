using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;

namespace SimpleBPM;

public class FlowEngine
{
    private readonly Dictionary<string, List<ProcessDefinition>> _definitions;
    private readonly Persistence.IProcessRepository? _repository;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;

    public FlowEngine(IEnumerable<ProcessDefinition> definitions, Persistence.IProcessRepository? repository = null, IEnumerable<INodeHandler>? handlers = null, IProcessEventHandler? eventHandler = null)
    {
        _definitions = new Dictionary<string, List<ProcessDefinition>>();
        _repository = repository;
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
        _handlers.TryAdd(NodeType.Interactive, new InteractiveNodeHandler());
        _handlers.TryAdd(NodeType.WaitUntilDate, new WaitUntilDateNodeHandler());
        _handlers.TryAdd(NodeType.WaitForSignal, new WaitForSignalNodeHandler());
        _handlers.TryAdd(NodeType.SubProcess, new SubProcessNodeHandler(repository, _handlers, eventHandler));
    }

    /// <summary>
    /// Internal constructor for sub-processes that share the parent's handler registry.
    /// </summary>
    internal FlowEngine(ProcessDefinition definition, Persistence.IProcessRepository? repository, Dictionary<NodeType, INodeHandler> handlers)
    {
        _definitions = new Dictionary<string, List<ProcessDefinition>>
        {
            [definition.Name] = new List<ProcessDefinition> { definition }
        };
        _repository = repository;
        _handlers = handlers;
    }

    public async Task<ProcessInstance> ExecuteAsync(ProcessInstance instance)
    {
        var definition = ResolveDefinition(instance);
        instance.DefinitionName ??= definition.Name;
        instance.DefinitionVersion ??= definition.Version;
        instance.LastExecutedAt = DateTime.UtcNow;

        if (_repository != null)
        {
            var existing = await _repository.GetProcessInstanceAsync(instance.ProcessId);
            if (existing == null)
            {
                await _repository.SaveProcessInstanceAsync(instance);
            }
        }

        var currentNodeId = instance.CurrentNodeId ?? definition.StartNodeId;

        while (!string.IsNullOrEmpty(currentNodeId))
        {
            var node = definition.GetNode(currentNodeId);

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

        return versions[^1];
    }

    private ProcessDefinition ResolveDefinition(ProcessInstance instance)
    {
        if (!string.IsNullOrEmpty(instance.DefinitionName) && !string.IsNullOrEmpty(instance.DefinitionVersion))
            return GetDefinition(instance.DefinitionName, instance.DefinitionVersion);

        if (!string.IsNullOrEmpty(instance.DefinitionName))
            return GetLatestDefinition(instance.DefinitionName);

        if (_definitions.Count == 1)
            return _definitions.Values.First()[^1];

        throw new InvalidOperationException("Cannot resolve process definition. Set DefinitionName on the instance.");
    }
}
