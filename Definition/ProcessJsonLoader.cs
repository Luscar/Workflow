using System.Text.Json;
using System.Text.Json.Serialization;
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
            ?? throw new InvalidOperationException("Invalid JSON process definition");

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
        var definition = new ProcessDefinition(jsonDef.Name);
        var nodesByName = new Dictionary<string, ProcessNode>();

        // Créer tous les nœuds
        foreach (var nodeDef in jsonDef.Nodes)
        {
            var node = CreateNode(nodeDef);
            nodesByName[nodeDef.Name] = node;
        }

        // Résoudre les connexions
        foreach (var nodeDef in jsonDef.Nodes)
        {
            var node = nodesByName[nodeDef.Name];

            if (nodeDef.Next != null)
            {
                foreach (var nextName in nodeDef.Next)
                {
                    if (nodesByName.TryGetValue(nextName, out var nextNode))
                    {
                        node.NextNodeIds.Add(nextNode.Id);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Node '{nextName}' not found");
                    }
                }
            }

            // Résoudre les routes pour DecisionNode
            if (node is DecisionNode decisionNode && nodeDef.Routes != null)
            {
                foreach (var route in nodeDef.Routes)
                {
                    if (nodesByName.TryGetValue(route.Value, out var targetNode))
                    {
                        decisionNode.ConditionToNodeId[route.Key] = targetNode.Id;
                        if (!decisionNode.NextNodeIds.Contains(targetNode.Id))
                            decisionNode.NextNodeIds.Add(targetNode.Id);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Node '{route.Value}' not found for route '{route.Key}'");
                    }
                }
            }

            definition.AddNode(node);
        }

        // Définir le nœud de départ
        if (!string.IsNullOrEmpty(jsonDef.StartNode))
        {
            if (nodesByName.TryGetValue(jsonDef.StartNode, out var startNode))
            {
                definition.StartNodeId = startNode.Id;
            }
        }
        else if (jsonDef.Nodes.Count > 0)
        {
            definition.StartNodeId = nodesByName[jsonDef.Nodes[0].Name].Id;
        }

        return definition;
    }

    private static ProcessNode CreateNode(NodeJsonDefinition nodeDef)
    {
        return nodeDef.Type switch
        {
            NodeType.Business => new BusinessNode(nodeDef.Command ?? nodeDef.Name, nodeDef.IsQuery)
            {
                Name = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.Decision => new DecisionNode(nodeDef.Query ?? nodeDef.Name)
            {
                Name = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.Interactive => new InteractiveNode
            {
                Name = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.WaitForSignal => new WaitForSignalNode(nodeDef.Signal ?? nodeDef.Name)
            {
                Name = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.WaitUntilDate => new WaitUntilDateNode(nodeDef.DateKey ?? "WaitUntilDate")
            {
                Name = nodeDef.DisplayName ?? nodeDef.Name
            },
            NodeType.SubProcess => throw new NotSupportedException("SubProcess nodes must be defined programmatically"),
            _ => throw new InvalidOperationException($"Unknown node type: {nodeDef.Type}")
        };
    }

    private static ProcessJsonDefinition BuildJsonDefinition(ProcessDefinition definition)
    {
        var jsonDef = new ProcessJsonDefinition
        {
            Name = definition.Name,
            Nodes = new List<NodeJsonDefinition>()
        };

        var nodesById = definition.Nodes.ToDictionary(n => n.Id, n => n);
        var nodeNames = definition.Nodes.ToDictionary(n => n.Id, n => n.Name);

        foreach (var node in definition.Nodes)
        {
            var nodeDef = new NodeJsonDefinition
            {
                Name = node.Name,
                Type = node.Type,
                DisplayName = node.Name
            };

            switch (node)
            {
                case BusinessNode bn:
                    nodeDef.Command = bn.CommandOrQueryName;
                    nodeDef.IsQuery = bn.IsQuery;
                    break;
                case DecisionNode dn:
                    nodeDef.Query = dn.QueryName;
                    nodeDef.Routes = dn.ConditionToNodeId.ToDictionary(
                        kvp => kvp.Key,
                        kvp => nodeNames.GetValueOrDefault(kvp.Value, kvp.Value));
                    break;
                case WaitForSignalNode wn:
                    nodeDef.Signal = wn.SignalName;
                    break;
                case WaitUntilDateNode wdn:
                    nodeDef.DateKey = wdn.DateKey;
                    break;
            }

            if (node.NextNodeIds.Count > 0 && node is not DecisionNode)
            {
                nodeDef.Next = node.NextNodeIds
                    .Select(id => nodeNames.GetValueOrDefault(id, id))
                    .ToList();
            }

            jsonDef.Nodes.Add(nodeDef);
        }

        // Trouver le nœud de départ
        var startNode = definition.Nodes.FirstOrDefault(n => n.Id == definition.StartNodeId);
        if (startNode != null)
        {
            jsonDef.StartNode = startNode.Name;
        }

        return jsonDef;
    }
}

public class ProcessJsonDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? StartNode { get; set; }
    public List<NodeJsonDefinition> Nodes { get; set; } = new();
}

public class NodeJsonDefinition
{
    public string Name { get; set; } = string.Empty;
    public NodeType Type { get; set; }
    public string? DisplayName { get; set; }

    // Business node
    public string? Command { get; set; }
    public bool IsQuery { get; set; }

    // Decision node
    public string? Query { get; set; }
    public Dictionary<string, string>? Routes { get; set; }

    // WaitForSignal node
    public string? Signal { get; set; }

    // WaitUntilDate node
    public string? DateKey { get; set; }

    // Connexions
    public List<string>? Next { get; set; }
}
