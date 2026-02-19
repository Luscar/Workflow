using SimpleBPM.Nodes;

namespace SimpleBPM.Definition;

public class ProcessBuilder
{
    private readonly ProcessDefinition _definition;
    private readonly Dictionary<string, ProcessNode> _nodesByName = new();
    private ProcessNode? _lastNode;
    private ProcessNode? _startNode;

    private ProcessBuilder(string processName, string version = "1.0")
    {
        _definition = new ProcessDefinition(processName, version);
    }

    public static ProcessBuilder Create(string processName, string version = "1.0") => new(processName, version);

    /// <summary>
    /// Ajoute un nœud métier (commande)
    /// </summary>
    public ProcessBuilder Business(string commandName, string? displayName = null)
    {
        var node = new BusinessNode(commandName) { Name = commandName, DisplayName = displayName ?? commandName };
        return AddNode(commandName, node);
    }

    /// <summary>
    /// Ajoute un nœud de décision avec ses routes
    /// </summary>
    public ProcessBuilder Decision(string queryName, string? displayName, Action<DecisionRouteBuilder> configureRoutes)
    {
        var node = new DecisionNode(queryName) { Name = queryName, DisplayName = displayName ?? queryName };
        var routeBuilder = new DecisionRouteBuilder(node);
        configureRoutes(routeBuilder);
        return AddNode(queryName, node);
    }

    /// <summary>
    /// Ajoute un nœud de décision avec ses routes
    /// </summary>
    public ProcessBuilder Decision(string queryName, Action<DecisionRouteBuilder> configureRoutes)
        => Decision(queryName, null, configureRoutes);

    /// <summary>
    /// Ajoute un nœud interactif (attente utilisateur)
    /// </summary>
    public ProcessBuilder Interactive(string name, string? displayName = null)
    {
        var node = new InteractiveNode { Name = name, DisplayName = displayName ?? name };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un nœud d'attente de signal
    /// </summary>
    public ProcessBuilder WaitForSignal(string signalName, string? displayName = null)
    {
        var node = new WaitForSignalNode(signalName) { Name = signalName, DisplayName = displayName ?? signalName };
        return AddNode(signalName, node);
    }

    /// <summary>
    /// Ajoute un nœud d'attente jusqu'à une date
    /// </summary>
    public ProcessBuilder WaitUntilDate(string name, string dateKey, string? displayName = null)
    {
        var node = new WaitUntilDateNode(dateKey) { Name = name, DisplayName = displayName ?? name };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un sous-processus
    /// </summary>
    public ProcessBuilder SubProcess(string name, ProcessDefinition subProcessDefinition,
        Dictionary<string, string>? inputMapping = null,
        Dictionary<string, string>? outputMapping = null,
        bool inheritAggregateId = true, string? displayName = null)
    {
        var node = new SubProcessNode(subProcessDefinition)
        {
            Name = name,
            DisplayName = displayName ?? name,
            InheritAggregateId = inheritAggregateId,
            InputMapping = inputMapping ?? new(),
            OutputMapping = outputMapping ?? new()
        };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un sous-processus défini via builder
    /// </summary>
    public ProcessBuilder SubProcess(string name, Action<ProcessBuilder> configureSubProcess,
        Dictionary<string, string>? inputMapping = null,
        Dictionary<string, string>? outputMapping = null,
        bool inheritAggregateId = true, string? displayName = null)
    {
        var subBuilder = new ProcessBuilder(name);
        configureSubProcess(subBuilder);
        var subDefinition = subBuilder.Build();

        var node = new SubProcessNode(subDefinition)
        {
            Name = name,
            DisplayName = displayName ?? name,
            InheritAggregateId = inheritAggregateId,
            InputMapping = inputMapping ?? new(),
            OutputMapping = outputMapping ?? new()
        };
        return AddNode(name, node);
    }

    /// <summary>
    /// Définit les paramètres du nœud courant
    /// </summary>
    public ProcessBuilder WithParameters(Dictionary<string, object> parameters)
    {
        if (_lastNode == null)
            throw new InvalidOperationException("No current node to set parameters on");

        foreach (var kvp in parameters)
        {
            _lastNode.Parameters[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Définit un paramètre sur le nœud courant
    /// </summary>
    public ProcessBuilder WithParameter(string key, object value)
    {
        if (_lastNode == null)
            throw new InvalidOperationException("No current node to set parameter on");

        _lastNode.Parameters[key] = value;
        return this;
    }

    /// <summary>
    /// Breaks the automatic node-linking chain. After calling this, the next node added
    /// will not be auto-linked from the previous node. Use this to define terminal nodes
    /// (e.g. RejectLoan) that should not flow into whatever node is declared next in the builder.
    /// </summary>
    public ProcessBuilder Break()
    {
        _lastNode = null;
        return this;
    }

    /// <summary>
    /// Connecte le nœud courant au nœud spécifié
    /// </summary>
    public ProcessBuilder Then(string nextNodeName)
    {
        if (_lastNode == null)
            throw new InvalidOperationException("No current node to connect from");

        _lastNode.NextNodeIds.Add(nextNodeName);
        return this;
    }

    /// <summary>
    /// Définit explicitement le nœud de départ
    /// </summary>
    public ProcessBuilder StartWith(string nodeName)
    {
        if (_nodesByName.TryGetValue(nodeName, out var node))
        {
            _startNode = node;
        }
        else
        {
            _startNode = null;
            _definition.StartNodeId = nodeName;
        }
        return this;
    }

    /// <summary>
    /// Construit la définition du processus
    /// </summary>
    public ProcessDefinition Build()
    {
        // Valider les références et ajouter les nœuds
        foreach (var node in _nodesByName.Values)
        {
            // Valider NextNodeIds
            foreach (var nextName in node.NextNodeIds)
            {
                if (!_nodesByName.ContainsKey(nextName))
                    throw new InvalidOperationException($"Node '{nextName}' not found");
            }

            // Valider les routes des DecisionNode
            if (node is DecisionNode decisionNode)
            {
                foreach (var kvp in decisionNode.ConditionToNodeId)
                {
                    if (!_nodesByName.ContainsKey(kvp.Value))
                        throw new InvalidOperationException($"Node '{kvp.Value}' not found for route '{kvp.Key}'");
                }
            }

            _definition.AddNode(node);
        }

        if (_startNode != null)
        {
            _definition.StartNodeId = _startNode.Name;
        }

        return _definition;
    }

    private ProcessBuilder AddNode(string name, ProcessNode node)
    {
        _nodesByName[name] = node;

        // Connecter automatiquement au nœud précédent (sauf pour les décisions qui ont leurs propres routes)
        if (_lastNode != null && _lastNode is not DecisionNode)
        {
            _lastNode.NextNodeIds.Add(node.Name);
        }

        // Le premier nœud devient le nœud de départ
        if (_startNode == null && _definition.StartNodeId == null)
        {
            _startNode = node;
            _definition.StartNodeId = node.Name;
        }

        _lastNode = node;
        return this;
    }
}

public class DecisionRouteBuilder
{
    private readonly DecisionNode _node;

    internal DecisionRouteBuilder(DecisionNode node)
    {
        _node = node;
    }

    public DecisionRouteBuilder When(string condition, string targetNodeName)
    {
        _node.AddRoute(condition, targetNodeName);
        return this;
    }
}
