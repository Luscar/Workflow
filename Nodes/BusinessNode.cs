namespace SimpleBPM.Nodes;

public class BusinessNode : ProcessNode
{
    public string CommandOrQueryName { get; set; }
    public bool IsQuery { get; set; }

    public BusinessNode(string commandOrQueryName, bool isQuery = false) : base(NodeType.Business)
    {
        CommandOrQueryName = commandOrQueryName;
        IsQuery = isQuery;
    }
}
