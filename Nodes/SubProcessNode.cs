namespace SimpleBPM.Nodes;

public class SubProcessNode : ProcessNode
{
    public ProcessDefinition SubProcessDefinition { get; set; }
    public bool InheritAggregateId { get; set; } = true;

    public SubProcessNode(ProcessDefinition subProcessDefinition) : base(NodeType.SubProcess)
    {
        SubProcessDefinition = subProcessDefinition;
    }
}
