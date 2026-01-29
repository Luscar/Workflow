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
        var node = new BusinessNode(commandName) { Name = displayName ?? commandName };
        return AddNode(commandName, node);
    }

    /// <summary>
    /// Ajoute un nœud métier (query)
    /// </summary>
    public ProcessBuilder Query(string queryName, string? displayName = null)
    {
        var node = new BusinessNode(queryName, isQuery: true) { Name = displayName ?? queryName };
        return AddNode(queryName, node);
    }

    /// <summary>
    /// Ajoute un nœud de décision avec ses routes
    /// </summary>
    public ProcessBuilder Decision(string queryName, string? displayName, Action<DecisionRouteBuilder> configureRoutes)
    {
        var node = new DecisionNode(queryName) { Name = displayName ?? queryName };
        var routeBuilder = new DecisionRouteBuilder(node, _nodesByName);
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
        var node = new InteractiveNode { Name = displayName ?? name };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un nœud d'attente de signal
    /// </summary>
    public ProcessBuilder WaitForSignal(string signalName, string? displayName = null)
    {
        var node = new WaitForSignalNode(signalName) { Name = displayName ?? signalName };
        return AddNode(signalName, node);
    }

    /// <summary>
    /// Ajoute un nœud d'attente jusqu'à une date
    /// </summary>
    public ProcessBuilder WaitUntilDate(string name, string dateKey, string? displayName = null)
    {
        var node = new WaitUntilDateNode(dateKey) { Name = displayName ?? name };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un sous-processus
    /// </summary>
    public ProcessBuilder SubProcess(string name, ProcessDefinition subProcessDefinition, bool inheritAggregateId = true, string? displayName = null)
    {
        var node = new SubProcessNode(subProcessDefinition)
        {
            Name = displayName ?? name,
            InheritAggregateId = inheritAggregateId
        };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un sous-processus défini via builder
    /// </summary>
    public ProcessBuilder SubProcess(string name, Action<ProcessBuilder> configureSubProcess, bool inheritAggregateId = true, string? displayName = null)
    {
        var subBuilder = new ProcessBuilder(name);
        configureSubProcess(subBuilder);
        var subDefinition = subBuilder.Build();

        var node = new SubProcessNode(subDefinition)
        {
            Name = displayName ?? name,
            InheritAggregateId = inheritAggregateId
        };
        return AddNode(name, node);
    }

    /// <summary>
    /// Connecte le nœud courant au nœud spécifié
    /// </summary>
    public ProcessBuilder Then(string nextNodeName)
    {
        if (_lastNode == null)
            throw new InvalidOperationException("No current node to connect from");

        // La connexion sera résolue à Build()
        _lastNode.NextNodeIds.Add($"@@{nextNodeName}");
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
            // Sera résolu à Build()
            _startNode = null;
            _definition.StartNodeId = $"@@{nodeName}";
        }
        return this;
    }

    /// <summary>
    /// Construit la définition du processus
    /// </summary>
    public ProcessDefinition Build()
    {
        // Résoudre toutes les références par nom
        foreach (var node in _nodesByName.Values)
        {
            // Résoudre NextNodeIds
            for (int i = 0; i < node.NextNodeIds.Count; i++)
            {
                var nextId = node.NextNodeIds[i];
                if (nextId.StartsWith("@@"))
                {
                    var nodeName = nextId[2..];
                    if (_nodesByName.TryGetValue(nodeName, out var targetNode))
                    {
                        node.NextNodeIds[i] = targetNode.Id;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Node '{nodeName}' not found");
                    }
                }
            }

            // Résoudre les routes des DecisionNode
            if (node is DecisionNode decisionNode)
            {
                var resolvedRoutes = new Dictionary<string, string>();
                foreach (var kvp in decisionNode.ConditionToNodeId)
                {
                    if (kvp.Value.StartsWith("@@"))
                    {
                        var nodeName = kvp.Value[2..];
                        if (_nodesByName.TryGetValue(nodeName, out var targetNode))
                        {
                            resolvedRoutes[kvp.Key] = targetNode.Id;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Node '{nodeName}' not found for route '{kvp.Key}'");
                        }
                    }
                    else
                    {
                        resolvedRoutes[kvp.Key] = kvp.Value;
                    }
                }
                decisionNode.ConditionToNodeId = resolvedRoutes;

                // Mettre à jour NextNodeIds pour DecisionNode
                decisionNode.NextNodeIds.Clear();
                foreach (var targetId in resolvedRoutes.Values)
                {
                    if (!decisionNode.NextNodeIds.Contains(targetId))
                        decisionNode.NextNodeIds.Add(targetId);
                }
            }

            _definition.AddNode(node);
        }

        // Résoudre le StartNodeId si nécessaire
        if (_definition.StartNodeId?.StartsWith("@@") == true)
        {
            var nodeName = _definition.StartNodeId[2..];
            if (_nodesByName.TryGetValue(nodeName, out var startNode))
            {
                _definition.StartNodeId = startNode.Id;
            }
        }
        else if (_startNode != null)
        {
            _definition.StartNodeId = _startNode.Id;
        }

        return _definition;
    }

    private ProcessBuilder AddNode(string name, ProcessNode node)
    {
        _nodesByName[name] = node;

        // Connecter automatiquement au nœud précédent (sauf pour les décisions qui ont leurs propres routes)
        if (_lastNode != null && _lastNode is not DecisionNode)
        {
            _lastNode.NextNodeIds.Add(node.Id);
        }

        // Le premier nœud devient le nœud de départ
        if (_startNode == null && _definition.StartNodeId == null)
        {
            _startNode = node;
            _definition.StartNodeId = node.Id;
        }

        _lastNode = node;
        return this;
    }
}

public class DecisionRouteBuilder
{
    private readonly DecisionNode _node;
    private readonly Dictionary<string, ProcessNode> _nodesByName;

    internal DecisionRouteBuilder(DecisionNode node, Dictionary<string, ProcessNode> nodesByName)
    {
        _node = node;
        _nodesByName = nodesByName;
    }

    public DecisionRouteBuilder When(string condition, string targetNodeName)
    {
        // Utiliser le marqueur @@ pour résolution ultérieure
        _node.AddRoute(condition, $"@@{targetNodeName}");
        return this;
    }
}
