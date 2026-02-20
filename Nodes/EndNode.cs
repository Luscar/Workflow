namespace SimpleBPM.Nodes;

/// <summary>
/// A terminal node that explicitly ends a process branch.
/// When the engine reaches an End node, the process is marked as Completed.
/// </summary>
public class EndNode : ProcessNode
{
    public EndNode() : base(NodeType.End) { }
}
