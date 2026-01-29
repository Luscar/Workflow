namespace SimpleBPM.Nodes;

public class DecisionNode : ProcessNode
{
    public string QueryName { get; set; }
    public Dictionary<string, string> ConditionToNodeId { get; set; } = new();

    public DecisionNode(string queryName) : base(NodeType.Decision)
    {
        QueryName = queryName;
    }

    public DecisionNode AddRoute(string condition, string targetNodeId)
    {
        ConditionToNodeId[condition] = targetNodeId;
        NextNodeIds.Add(targetNodeId);
        return this;
    }
}
