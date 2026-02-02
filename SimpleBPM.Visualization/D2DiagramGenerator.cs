using System.Text;
using SimpleBPM.Nodes;

namespace SimpleBPM.Visualization;

/// <summary>
/// Generates D2 diagrams from process definitions.
/// D2 files can be rendered to SVG/PNG using the d2 CLI tool (https://d2lang.com).
/// </summary>
public class D2DiagramGenerator : IDiagramGenerator
{
    public string Format => "D2";

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

        // Direction
        var direction = options.Direction == DiagramDirection.LeftToRight ? "right" : "down";
        sb.AppendLine($"direction: {direction}");
        sb.AppendLine();

        // Title
        if (options.ShowTitle)
        {
            sb.AppendLine($"title: \"{EscapeD2(definition.Name)}\" {{");
            sb.AppendLine("  near: top-center");
            sb.AppendLine("  shape: text");
            sb.AppendLine("  style.font-size: 24");
            sb.AppendLine("  style.bold: true");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Compute instance state for node styling
        var nodeStates = instance != null ? ComputeNodeStates(definition, instance) : null;

        // Style classes
        AppendClasses(sb, instance != null);
        sb.AppendLine();

        // Node aliases (D2 uses identifiers, so we need clean keys)
        var nodeAliases = new Dictionary<string, string>();
        int aliasIndex = 0;

        // Start marker
        if (!string.IsNullOrEmpty(definition.StartNodeId))
        {
            sb.AppendLine("start: \"\" {");
            sb.AppendLine("  shape: circle");
            sb.AppendLine("  style.fill: \"#333\"");
            sb.AppendLine("  style.stroke: \"#333\"");
            sb.AppendLine("  width: 30");
            sb.AppendLine("  height: 30");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Nodes
        foreach (var (nodeId, node) in definition.Nodes)
        {
            var alias = $"n{aliasIndex++}";
            nodeAliases[nodeId] = alias;

            var label = BuildNodeLabel(node, options);
            var shape = GetD2Shape(node.Type);
            var styleClass = GetNodeClass(node, nodeId, nodeStates);

            sb.AppendLine($"{alias}: \"{label}\" {{");
            sb.AppendLine($"  shape: {shape}");
            sb.AppendLine($"  class: {styleClass}");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Start edge
        if (!string.IsNullOrEmpty(definition.StartNodeId) && nodeAliases.TryGetValue(definition.StartNodeId, out var startTarget))
        {
            sb.AppendLine($"start -> {startTarget}");
        }

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
                        sb.AppendLine($"{fromAlias} -> {toAlias}: \"{EscapeD2(condition)}\"");
                    }
                }
            }
            else
            {
                foreach (var nextId in node.NextNodeIds)
                {
                    if (nodeAliases.TryGetValue(nextId, out var toAlias))
                    {
                        sb.AppendLine($"{fromAlias} -> {toAlias}");
                    }
                }
            }
        }

        // Legend
        if (options.ShowLegend)
        {
            AppendLegend(sb, instance != null);
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildNodeLabel(ProcessNode node, DiagramOptions options)
    {
        var label = EscapeD2(node.Name);

        if (!options.ShowNodeDetails)
            return label;

        var detail = node switch
        {
            BusinessNode b => $"\\n{EscapeD2(b.CommandName)}",
            DecisionNode d => $"\\n{EscapeD2(d.QueryName)}",
            WaitForSignalNode s => $"\\nsignal: {EscapeD2(s.SignalName)}",
            WaitUntilDateNode w when w.DateKey != null => $"\\ndate: {EscapeD2(w.DateKey)}",
            SubProcessNode sp => $"\\nsub: {EscapeD2(sp.SubProcessDefinition.Name)}",
            _ => ""
        };

        return label + detail;
    }

    private static string GetD2Shape(NodeType type)
    {
        return type switch
        {
            NodeType.Decision => "diamond",
            NodeType.Interactive => "parallelogram",
            NodeType.WaitUntilDate => "oval",
            NodeType.WaitForSignal => "oval",
            NodeType.SubProcess => "page",
            _ => "rectangle"
        };
    }

    private static string GetNodeClass(ProcessNode node, string nodeId, Dictionary<string, NodeInstanceState>? nodeStates)
    {
        if (nodeStates != null && nodeStates.TryGetValue(nodeId, out var state))
        {
            return state switch
            {
                NodeInstanceState.Current => "current",
                NodeInstanceState.Completed => "completed",
                NodeInstanceState.Failed => "failed",
                _ => GetTypeClass(node.Type)
            };
        }

        return GetTypeClass(node.Type);
    }

    private static string GetTypeClass(NodeType type)
    {
        return type switch
        {
            NodeType.Business => "business",
            NodeType.Decision => "decision",
            NodeType.Interactive => "interactive",
            NodeType.WaitUntilDate => "wait",
            NodeType.WaitForSignal => "wait",
            NodeType.SubProcess => "subprocess",
            _ => "business"
        };
    }

    private static void AppendClasses(StringBuilder sb, bool includeInstanceStyles)
    {
        sb.AppendLine("classes: {");
        sb.AppendLine("  business: {");
        sb.AppendLine("    style.fill: \"#4a90d9\"");
        sb.AppendLine("    style.stroke: \"#2c5f8a\"");
        sb.AppendLine("    style.font-color: \"#fff\"");
        sb.AppendLine("  }");
        sb.AppendLine("  decision: {");
        sb.AppendLine("    style.fill: \"#f5a623\"");
        sb.AppendLine("    style.stroke: \"#c17d0e\"");
        sb.AppendLine("    style.font-color: \"#fff\"");
        sb.AppendLine("  }");
        sb.AppendLine("  interactive: {");
        sb.AppendLine("    style.fill: \"#7ed321\"");
        sb.AppendLine("    style.stroke: \"#5a9a18\"");
        sb.AppendLine("    style.font-color: \"#fff\"");
        sb.AppendLine("  }");
        sb.AppendLine("  wait: {");
        sb.AppendLine("    style.fill: \"#9b59b6\"");
        sb.AppendLine("    style.stroke: \"#7d3c98\"");
        sb.AppendLine("    style.font-color: \"#fff\"");
        sb.AppendLine("  }");
        sb.AppendLine("  subprocess: {");
        sb.AppendLine("    style.fill: \"#1abc9c\"");
        sb.AppendLine("    style.stroke: \"#148f77\"");
        sb.AppendLine("    style.font-color: \"#fff\"");
        sb.AppendLine("  }");

        if (includeInstanceStyles)
        {
            sb.AppendLine("  current: {");
            sb.AppendLine("    style.fill: \"#ff9800\"");
            sb.AppendLine("    style.stroke: \"#e65100\"");
            sb.AppendLine("    style.font-color: \"#fff\"");
            sb.AppendLine("    style.stroke-width: 3");
            sb.AppendLine("  }");
            sb.AppendLine("  completed: {");
            sb.AppendLine("    style.fill: \"#4caf50\"");
            sb.AppendLine("    style.stroke: \"#2e7d32\"");
            sb.AppendLine("    style.font-color: \"#fff\"");
            sb.AppendLine("  }");
            sb.AppendLine("  failed: {");
            sb.AppendLine("    style.fill: \"#f44336\"");
            sb.AppendLine("    style.stroke: \"#c62828\"");
            sb.AppendLine("    style.font-color: \"#fff\"");
            sb.AppendLine("  }");
        }

        sb.AppendLine("}");
    }

    private static void AppendLegend(StringBuilder sb, bool includeInstanceStyles)
    {
        sb.AppendLine();
        sb.AppendLine("legend: \"Legend\" {");
        sb.AppendLine("  l1: \"Business\" { class: business; shape: rectangle }");
        sb.AppendLine("  l2: \"Decision\" { class: decision; shape: diamond }");
        sb.AppendLine("  l3: \"Interactive\" { class: interactive; shape: parallelogram }");
        sb.AppendLine("  l4: \"Wait\" { class: wait; shape: oval }");
        sb.AppendLine("  l5: \"SubProcess\" { class: subprocess; shape: page }");

        if (includeInstanceStyles)
        {
            sb.AppendLine("  l6: \"Current\" { class: current; shape: rectangle }");
            sb.AppendLine("  l7: \"Completed\" { class: completed; shape: rectangle }");
            sb.AppendLine("  l8: \"Failed\" { class: failed; shape: rectangle }");
        }

        sb.AppendLine("}");
    }

    private static Dictionary<string, NodeInstanceState> ComputeNodeStates(
        ProcessDefinition definition, ProcessInstance instance)
    {
        var states = new Dictionary<string, NodeInstanceState>();

        foreach (var history in instance.ExecutionHistory)
        {
            if (definition.Nodes.ContainsKey(history.NodeId))
            {
                states[history.NodeId] = history.Success
                    ? NodeInstanceState.Completed
                    : NodeInstanceState.Failed;
            }
        }

        if (!string.IsNullOrEmpty(instance.CurrentNodeId)
            && definition.Nodes.ContainsKey(instance.CurrentNodeId)
            && instance.Status != ProcessStatus.Completed)
        {
            states[instance.CurrentNodeId] = NodeInstanceState.Current;
        }

        return states;
    }

    private static string EscapeD2(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
