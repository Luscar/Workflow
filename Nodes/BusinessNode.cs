namespace SimpleBPM.Nodes;

public class BusinessNode : NodeDefinition
{
    public string CommandName { get; set; }

    public BusinessNode(string commandName) : base(NodeType.Business)
    {
        CommandName = commandName;
    }
}
