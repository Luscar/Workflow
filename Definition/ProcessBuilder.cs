using SimpleBPM.Nodes;

namespace SimpleBPM.Definition;

public class ProcessBuilder
{
    private readonly ProcessDefinition _definition;
    private readonly Dictionary<string, NodeDefinition> _nodesByName = new();
    private NodeDefinition? _lastNode;
    private NodeDefinition? _startNode;

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
    /// Ajoute un nœud d'attente jusqu'à une date statique
    /// </summary>
    public ProcessBuilder WaitUntilDate(string name, DateTime targetDate, string? displayName = null)
    {
        var node = new WaitUntilDateNode(targetDate) { Name = name, DisplayName = displayName ?? name };
        return AddNode(name, node);
    }

    /// <summary>
    /// Ajoute un nœud d'attente jusqu'à une date calculée dynamiquement
    /// </summary>
    public ProcessBuilder WaitUntilDate(string name, Func<ProcessInstance, DateTime> dateProvider, string? displayName = null)
    {
        var node = new WaitUntilDateNode(dateProvider) { Name = name, DisplayName = displayName ?? name };
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
            throw new InvalidOperationException("Aucun nœud courant sur lequel définir les paramètres");

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
            throw new InvalidOperationException("Aucun nœud courant sur lequel définir le paramètre");

        _lastNode.Parameters[key] = value;
        return this;
    }

    /// <summary>
    /// Définit la commande à exécuter lorsqu'un nœud bloquant est atteint
    /// </summary>
    public ProcessBuilder WithOnEnterCommand(string commandName)
    {
        if (_lastNode == null)
            throw new InvalidOperationException("Aucun nœud courant sur lequel définir la commande OnEnter");

        _lastNode.OnEnterCommandName = commandName;
        return this;
    }

    /// <summary>
    /// Ajoute un paramètre à la commande OnEnter du nœud courant
    /// </summary>
    public ProcessBuilder WithOnEnterCommandParameter(string key, object value)
    {
        if (_lastNode == null)
            throw new InvalidOperationException("Aucun nœud courant sur lequel définir le paramètre de commande OnEnter");

        _lastNode.OnEnterCommandParameters[key] = value;
        return this;
    }

    /// <summary>
    /// Ajoute un nœud terminal explicite pour terminer une branche du processus.
    /// Le prédécesseur de la branche est lié à ce nœud, et la chaîne de liaison automatique est rompue
    /// afin que le nœud suivant déclaré commence une nouvelle section indépendante.
    /// </summary>
    public ProcessBuilder End(string name, string? displayName = null)
    {
        var node = new EndNode { Name = name, DisplayName = displayName ?? name };
        return AddNode(name, node);
    }

    /// <summary>
    /// Rompt la chaîne de liaison automatique des nœuds. Après cet appel, le nœud suivant ajouté
    /// ne sera pas lié automatiquement au nœud précédent.
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
            throw new InvalidOperationException("Aucun nœud courant depuis lequel se connecter");

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
                    throw new InvalidOperationException($"Nœud '{nextName}' introuvable");
            }

            // Valider les routes des DecisionNode
            if (node is DecisionNode decisionNode)
            {
                foreach (var kvp in decisionNode.ConditionToNodeId)
                {
                    if (!_nodesByName.ContainsKey(kvp.Value))
                        throw new InvalidOperationException($"Nœud '{kvp.Value}' introuvable pour la route '{kvp.Key}'");
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

    private ProcessBuilder AddNode(string name, NodeDefinition node)
    {
        _nodesByName[name] = node;

        // Liaison automatique au nœud précédent seulement si :
        // - il existe un nœud précédent
        // - ce n'est pas un DecisionNode (qui gère ses propres routes)
        // - il n'a pas encore de nœud suivant déclaré explicitement (ex. via .Then())
        if (_lastNode != null && _lastNode is not DecisionNode && _lastNode.NextNodeIds.Count == 0)
        {
            _lastNode.NextNodeIds.Add(node.Name);
        }

        // Le premier nœud devient le nœud de départ
        if (_startNode == null && _definition.StartNodeId == null)
        {
            _startNode = node;
            _definition.StartNodeId = node.Name;
        }

        // Les nœuds End sont terminaux : rompre la chaîne pour qu'aucun nœud ne soit lié automatiquement après eux
        _lastNode = node is EndNode ? null : node;
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
