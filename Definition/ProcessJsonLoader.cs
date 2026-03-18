using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleBPM;
using SimpleBPM.Nodes;

namespace SimpleBPM.Definition;

/// <summary>
/// Charge une définition de processus depuis JSON
/// </summary>
public static class ProcessJsonLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Charge un processus depuis une chaîne JSON
    /// </summary>
    public static ProcessDefinition FromJson(string json)
    {
        var jsonDef = JsonSerializer.Deserialize<ProcessJsonDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException("Définition JSON du processus invalide");

        return BuildFromJson(jsonDef);
    }

    /// <summary>
    /// Charge un processus depuis un fichier JSON
    /// </summary>
    public static ProcessDefinition FromJsonFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }

    /// <summary>
    /// Exporte une définition de processus en JSON
    /// </summary>
    public static string ToJson(ProcessDefinition definition, bool indented = true)
    {
        var jsonDef = BuildJsonDefinition(definition);
        return JsonSerializer.Serialize(jsonDef, new JsonSerializerOptions
        {
            WriteIndented = indented,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    private static ProcessDefinition BuildFromJson(ProcessJsonDefinition jsonDef)
    {
        var definition = new ProcessDefinition(jsonDef.Name, jsonDef.Version ?? "1.0");
        var nodeNames = new HashSet<string>();

        // Créer tous les nœuds
        foreach (var nodeDef in jsonDef.Nodes)
        {
            nodeNames.Add(nodeDef.Name);
        }

        // Créer et connecter les nœuds
        foreach (var nodeDef in jsonDef.Nodes)
        {
            var node = CreateNode(nodeDef);

            if (nodeDef.Next != null)
            {
                foreach (var nextName in nodeDef.Next)
                {
                    if (!nodeNames.Contains(nextName))
                        throw new InvalidOperationException($"Nœud '{nextName}' introuvable");

                    node.NextNodeIds.Add(nextName);
                }
            }

            // Résoudre les routes pour DecisionNode
            if (node is DecisionNode decisionNode)
            {
                if (nodeDef.Routes != null)
                {
                    foreach (var route in nodeDef.Routes)
                    {
                        if (!nodeNames.Contains(route.Value))
                            throw new InvalidOperationException($"Nœud '{route.Value}' introuvable pour la route '{route.Key}'");

                        decisionNode.ConditionToNodeId[route.Key] = route.Value;
                        if (!decisionNode.NextNodeIds.Contains(route.Value))
                            decisionNode.NextNodeIds.Add(route.Value);
                    }
                }

                if (nodeDef.Conditions != null)
                {
                    foreach (var cond in nodeDef.Conditions)
                    {
                        if (!nodeNames.Contains(cond.NoeudCible))
                            throw new InvalidOperationException($"Nœud '{cond.NoeudCible}' introuvable pour la condition sur '{cond.NomVariable}'");

                        decisionNode.AddCondition(cond.NomVariable, cond.Valeur, cond.Operateur, cond.TypeDonnee, cond.NoeudCible);
                    }
                }

                if (!string.IsNullOrEmpty(nodeDef.DefaultNode))
                {
                    if (!nodeNames.Contains(nodeDef.DefaultNode))
                        throw new InvalidOperationException($"Nœud par défaut '{nodeDef.DefaultNode}' introuvable");

                    decisionNode.SetNoeudParDefaut(nodeDef.DefaultNode);
                }
            }

            definition.AddNode(node);
        }

        // Définir le nœud de départ
        if (!string.IsNullOrEmpty(jsonDef.StartNode))
        {
            definition.StartNodeId = jsonDef.StartNode;
        }
        else if (jsonDef.Nodes.Count > 0)
        {
            definition.StartNodeId = jsonDef.Nodes[0].Name;
        }

        return definition;
    }

    private static NodeDefinition CreateNode(NodeJsonDefinition nodeDef)
    {
        var node = nodeDef.Type switch
        {
            NodeType.Business => new BusinessNode(nodeDef.Command ?? nodeDef.Name)
            {
                Name = nodeDef.Name,
                DisplayName = nodeDef.DisplayName ?? nodeDef.Name
            } as NodeDefinition,
            NodeType.Decision => new DecisionNode(nodeDef.Query ?? nodeDef.Name)
            {
                Name = nodeDef.Name,
                DisplayName = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.Interactive => new InteractiveNode
            {
                Name = nodeDef.Name,
                DisplayName = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.WaitForSignal => new WaitForSignalNode(nodeDef.Signal ?? nodeDef.Name)
            {
                Name = nodeDef.Name,
                DisplayName = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.WaitUntilDate => !string.IsNullOrEmpty(nodeDef.DateQuery)
                ? new WaitUntilDateNode
                {
                    Name = nodeDef.Name,
                    DisplayName = nodeDef.DisplayName ?? nodeDef.Name,
                    DateQueryName = nodeDef.DateQuery,
                    DateQueryParameters = nodeDef.DateQueryParameters ?? new()
                }
                : new WaitUntilDateNode(nodeDef.DateKey ?? "WaitUntilDate")
                {
                    Name = nodeDef.Name,
                    DisplayName = nodeDef.DisplayName ?? nodeDef.Name
                },
            NodeType.SubProcess => CreateSubProcessNode(nodeDef),
            NodeType.End => new EndNode
            {
                Name = nodeDef.Name,
                DisplayName = nodeDef.DisplayName ?? nodeDef.Name
            },
            _ => throw new InvalidOperationException($"Type de nœud inconnu : {nodeDef.Type}")
        };

        if (nodeDef.Parameters != null)
        {
            foreach (var kvp in nodeDef.Parameters)
            {
                node.Parameters[kvp.Key] = kvp.Value;
            }
        }

        return node;
    }

    private static NodeDefinition CreateSubProcessNode(NodeJsonDefinition nodeDef)
    {
        if (nodeDef.SubProcess == null)
            throw new InvalidOperationException($"Le nœud SubProcess '{nodeDef.Name}' nécessite une définition 'subProcess'");

        var subDefinition = BuildFromJson(nodeDef.SubProcess);

        return new SubProcessNode(subDefinition)
        {
            Name = nodeDef.Name,
            DisplayName = nodeDef.DisplayName ?? nodeDef.Name,
            InheritAggregateId = nodeDef.InheritAggregateId ?? true,
            InputMapping = nodeDef.InputMapping ?? new(),
            OutputMapping = nodeDef.OutputMapping ?? new()
        };
    }

    private static ProcessJsonDefinition BuildJsonDefinition(ProcessDefinition definition)
    {
        var jsonDef = new ProcessJsonDefinition
        {
            Name = definition.Name,
            Version = definition.Version,
            Nodes = new List<NodeJsonDefinition>()
        };

        foreach (var node in definition.Nodes.Values)
        {
            var nodeDef = new NodeJsonDefinition
            {
                Name = node.Name,
                Type = node.Type,
                DisplayName = node.DisplayName
            };

            switch (node)
            {
                case BusinessNode bn:
                    nodeDef.Command = bn.CommandName;
                    break;
                case DecisionNode dn:
                    nodeDef.Query = dn.QueryName;
                    if (dn.ConditionToNodeId.Count > 0)
                        nodeDef.Routes = new Dictionary<string, string>(dn.ConditionToNodeId);
                    if (dn.Conditions.Count > 0)
                        nodeDef.Conditions = dn.Conditions.Select(c => new ConditionDecisionJson
                        {
                            NomVariable = c.NomVariable,
                            Valeur = c.Valeur,
                            Operateur = c.Operateur,
                            TypeDonnee = c.TypeDonnee,
                            NoeudCible = c.NoeudCible
                        }).ToList();
                    if (!string.IsNullOrEmpty(dn.NoeudParDefaut))
                        nodeDef.DefaultNode = dn.NoeudParDefaut;
                    break;
                case WaitForSignalNode wn:
                    nodeDef.Signal = wn.SignalName;
                    break;
                case WaitUntilDateNode wdn:
                    nodeDef.DateKey = wdn.DateKey;
                    nodeDef.DateQuery = wdn.DateQueryName;
                    if (wdn.DateQueryParameters.Count > 0)
                        nodeDef.DateQueryParameters = wdn.DateQueryParameters;
                    break;
                case SubProcessNode spn:
                    nodeDef.SubProcess = BuildJsonDefinition(spn.SubProcessDefinition);
                    nodeDef.InheritAggregateId = spn.InheritAggregateId;
                    if (spn.InputMapping.Count > 0)
                        nodeDef.InputMapping = spn.InputMapping;
                    if (spn.OutputMapping.Count > 0)
                        nodeDef.OutputMapping = spn.OutputMapping;
                    break;
                case EndNode:
                    // Les nœuds End n'ont pas de propriétés supplémentaires
                    break;
            }

            if (node.Parameters.Count > 0)
            {
                nodeDef.Parameters = new Dictionary<string, object>(node.Parameters);
            }

            if (node.NextNodeIds.Count > 0 && node is not DecisionNode)
            {
                nodeDef.Next = new List<string>(node.NextNodeIds);
            }

            jsonDef.Nodes.Add(nodeDef);
        }

        jsonDef.StartNode = definition.StartNodeId;

        return jsonDef;
    }
}

public class ProcessJsonDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? StartNode { get; set; }
    public List<NodeJsonDefinition> Nodes { get; set; } = new();
}

public class NodeJsonDefinition
{
    public string Name { get; set; } = string.Empty;
    public NodeType Type { get; set; }
    public string? DisplayName { get; set; }

    // Nœud métier
    public string? Command { get; set; }

    // Nœud de décision
    public string? Query { get; set; }
    public Dictionary<string, string>? Routes { get; set; }
    public List<ConditionDecisionJson>? Conditions { get; set; }
    public string? DefaultNode { get; set; }

    // Nœud WaitForSignal
    public string? Signal { get; set; }

    // Nœud WaitUntilDate
    public string? DateKey { get; set; }
    public string? DateQuery { get; set; }
    public Dictionary<string, object>? DateQueryParameters { get; set; }

    // Nœud SubProcess
    public ProcessJsonDefinition? SubProcess { get; set; }
    public bool? InheritAggregateId { get; set; }
    public Dictionary<string, string>? InputMapping { get; set; }
    public Dictionary<string, string>? OutputMapping { get; set; }

    // Paramètres du nœud
    public Dictionary<string, object>? Parameters { get; set; }

    // Connexions
    public List<string>? Next { get; set; }
}

public class ConditionDecisionJson
{
    public string NomVariable { get; set; } = string.Empty;
    public object? Valeur { get; set; }
    public OperateurFiltre Operateur { get; set; } = OperateurFiltre.Egal;
    public TypeDonnee TypeDonnee { get; set; } = TypeDonnee.Texte;
    public string NoeudCible { get; set; } = string.Empty;
}
