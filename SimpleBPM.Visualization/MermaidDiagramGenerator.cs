using System.Text;
using SimpleBPM.Nodes;

namespace SimpleBPM.Visualization;

/// <summary>
/// Generates Mermaid flowchart diagrams from process definitions.
/// Output can be rendered in GitHub markdown, VS Code, Mermaid Live Editor, etc.
/// </summary>
public class MermaidDiagramGenerator : IDiagramGenerator
{
    public string Format => "Mermaid";

    public string Generate(ProcessDefinition definition, DiagramOptions? options = null)
    {
        return GenerateInternal(definition, instance: null, options ?? new DiagramOptions());
    }

    public string Generate(ProcessDefinition definition, ProcessInstance instance, DiagramOptions? options = null)
    {
        return GenerateInternal(definition, instance, options ?? new DiagramOptions());
    }

    private string GenerateInternal(ProcessDefinition definition, ProcessInstance? instance, DiagramOptions options)
    {
        var sb = new StringBuilder();
        var direction = options.Direction == DiagramDirection.LeftToRight ? "LR" : "TD";

        // Header
        if (options.ShowTitle)
        {
            sb.AppendLine("---");
            sb.AppendLine($"title: {definition.Name}");
            sb.AppendLine("---");
        }

        sb.AppendLine($"flowchart {direction}");

        // Compute instance state for node styling
        var nodeStates = instance != null ? ComputeNodeStates(definition, instance) : null;

        // Style classes
        AppendStyleClasses(sb, instance != null);

        sb.AppendLine();

        // Nodes
        var nodeAliases = new Dictionary<string, string>();
        int aliasIndex = 0;

        foreach (var (nodeId, node) in definition.Nodes)
        {
            var alias = $"n{aliasIndex++}";
            nodeAliases[nodeId] = alias;

            var label = BuildNodeLabel(node, options);
            var shape = GetMermaidShape(node, alias, label);
            sb.AppendLine($"    {shape}");
        }

        sb.AppendLine();

        // Edges
        foreach (var (nodeId, node) in definition.Nodes)
        {
            var fromAlias = nodeAliases[nodeId];

            if (node is DecisionNode decision && options.ShowConditionLabels)
            {
                foreach (var (condition, targetId) in decision.ConditionToNodeId)
                {
                    if (nodeAliases.TryGetValue(targetId, out var toAlias))
                    {
                        var escapedCondition = EscapeLabel(condition);
                        sb.AppendLine($"    {fromAlias} -->|\"{escapedCondition}\"| {toAlias}");
                    }
                }
            }
            else
            {
                foreach (var nextId in node.NextNodeIds)
                {
                    if (nodeAliases.TryGetValue(nextId, out var toAlias))
                    {
                        sb.AppendLine($"    {fromAlias} --> {toAlias}");
                    }
                }
            }
        }

        // Apply style classes
        sb.AppendLine();
        foreach (var (nodeId, node) in definition.Nodes)
        {
            var alias = nodeAliases[nodeId];
            var cssClass = GetNodeStyleClass(node, nodeId, nodeStates);
            sb.AppendLine($"    class {alias} {cssClass}");
        }

        // Mark the start node
        if (!string.IsNullOrEmpty(definition.StartNodeId) && nodeAliases.TryGetValue(definition.StartNodeId, out var startAlias))
        {
            // Add a start marker
            sb.AppendLine();
            sb.AppendLine($"    start(( )):::startNode --> {startAlias}");
        }

        if (options.ShowLegend)
        {
            AppendLegend(sb, instance != null);
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildNodeLabel(ProcessNode node, DiagramOptions options)
    {
        var label = EscapeLabel(node.Name);

        if (!options.ShowNodeDetails)
            return label;

        var detail = node switch
        {
            BusinessNode b => $"<br/><small>{EscapeLabel(b.CommandName)}</small>",
            DecisionNode d => $"<br/><small>{EscapeLabel(d.QueryName)}</small>",
            WaitForSignalNode s => $"<br/><small>signal: {EscapeLabel(s.SignalName)}</small>",
            WaitUntilDateNode w when w.DateKey != null => $"<br/><small>date: {EscapeLabel(w.DateKey)}</small>",
            SubProcessNode sp => $"<br/><small>sub: {EscapeLabel(sp.SubProcessDefinition.Name)}</small>",
            _ => ""
        };

        return label + detail;
    }

    private static string GetMermaidShape(ProcessNode node, string alias, string label)
    {
        return node.Type switch
        {
            NodeType.Decision => $"{alias}{{\"{label}\"}}",
            NodeType.Interactive => $"{alias}[[\"{label}\"]]",
            NodeType.WaitUntilDate => $"{alias}([\"{label}\"])",
            NodeType.WaitForSignal => $"{alias}([\"{label}\"])",
            NodeType.SubProcess => $"{alias}[[\"{label}\"]]",
            _ => $"{alias}[\"{label}\"]"
        };
    }

    private static string GetNodeStyleClass(ProcessNode node, string nodeId, Dictionary<string, NodeInstanceState>? nodeStates)
    {
        // Instance state takes priority for styling
        if (nodeStates != null && nodeStates.TryGetValue(nodeId, out var state))
        {
            return state switch
            {
                NodeInstanceState.Current => "currentNode",
                NodeInstanceState.Completed => "completedNode",
                NodeInstanceState.Failed => "failedNode",
                _ => GetTypeStyleClass(node.Type)
            };
        }

        return GetTypeStyleClass(node.Type);
    }

    private static string GetTypeStyleClass(NodeType type)
    {
        return type switch
        {
            NodeType.Business => "businessNode",
            NodeType.Decision => "decisionNode",
            NodeType.Interactive => "interactiveNode",
            NodeType.WaitUntilDate => "waitNode",
            NodeType.WaitForSignal => "waitNode",
            NodeType.SubProcess => "subProcessNode",
            _ => "businessNode"
        };
    }

    private static void AppendStyleClasses(StringBuilder sb, bool includeInstanceStyles)
    {
        sb.AppendLine();
        sb.AppendLine("    classDef businessNode fill:#4a90d9,stroke:#2c5f8a,color:#fff");
        sb.AppendLine("    classDef decisionNode fill:#f5a623,stroke:#c17d0e,color:#fff");
        sb.AppendLine("    classDef interactiveNode fill:#7ed321,stroke:#5a9a18,color:#fff");
        sb.AppendLine("    classDef waitNode fill:#9b59b6,stroke:#7d3c98,color:#fff");
        sb.AppendLine("    classDef subProcessNode fill:#1abc9c,stroke:#148f77,color:#fff");
        sb.AppendLine("    classDef startNode fill:#333,stroke:#333,color:#fff");

        if (includeInstanceStyles)
        {
            sb.AppendLine("    classDef currentNode fill:#ff9800,stroke:#e65100,color:#fff,stroke-width:3px");
            sb.AppendLine("    classDef completedNode fill:#4caf50,stroke:#2e7d32,color:#fff");
            sb.AppendLine("    classDef failedNode fill:#f44336,stroke:#c62828,color:#fff");
        }
    }

    private static void AppendLegend(StringBuilder sb, bool includeInstanceStyles)
    {
        sb.AppendLine();
        sb.AppendLine("    subgraph Legend");
        sb.AppendLine("        direction LR");
        sb.AppendLine("        l1[\"Business\"]:::businessNode");
        sb.AppendLine("        l2{\"Decision\"}:::decisionNode");
        sb.AppendLine("        l3[[\"Interactive\"]]:::interactiveNode");
        sb.AppendLine("        l4([\"Wait\"]):::waitNode");
        sb.AppendLine("        l5[[\"SubProcess\"]]:::subProcessNode");

        if (includeInstanceStyles)
        {
            sb.AppendLine("        l6[\"Current\"]:::currentNode");
            sb.AppendLine("        l7[\"Completed\"]:::completedNode");
            sb.AppendLine("        l8[\"Failed\"]:::failedNode");
        }

        sb.AppendLine("    end");
    }

    private static Dictionary<string, NodeInstanceState> ComputeNodeStates(
        ProcessDefinition definition, ProcessInstance instance)
    {
        var states = new Dictionary<string, NodeInstanceState>();

        // Mark completed/failed nodes from execution history
        foreach (var history in instance.ExecutionHistory)
        {
            if (definition.Nodes.ContainsKey(history.NodeId))
            {
                states[history.NodeId] = history.Success
                    ? NodeInstanceState.Completed
                    : NodeInstanceState.Failed;
            }
        }

        // Mark current node (overrides history if re-visited)
        if (!string.IsNullOrEmpty(instance.CurrentNodeId)
            && definition.Nodes.ContainsKey(instance.CurrentNodeId)
            && instance.Status != ProcessStatus.Completed)
        {
            states[instance.CurrentNodeId] = NodeInstanceState.Current;
        }

        return states;
    }

    private static string EscapeLabel(string text)
    {
        return text.Replace("\"", "#quot;");
    }
}
