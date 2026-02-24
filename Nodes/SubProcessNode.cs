namespace SimpleBPM.Nodes;

public class SubProcessNode : NodeDefinition
{
    public ProcessDefinition SubProcessDefinition { get; set; }
    public bool InheritAggregateId { get; set; } = true;

    /// <summary>
    /// Maps parent variable names to sub-process variable names.
    /// Key = parent variable, Value = sub-process variable.
    /// </summary>
    public Dictionary<string, string> InputMapping { get; set; } = new();

    /// <summary>
    /// Maps sub-process variable names back to parent variable names.
    /// Key = sub-process variable, Value = parent variable.
    /// </summary>
    public Dictionary<string, string> OutputMapping { get; set; } = new();

    public SubProcessNode(ProcessDefinition subProcessDefinition) : base(NodeType.SubProcess)
    {
        SubProcessDefinition = subProcessDefinition;
    }
}
