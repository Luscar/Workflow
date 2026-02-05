namespace SimpleBPM;

public enum NodeType
{
    Business,
    Decision,
    Interactive,
    WaitUntilDate,
    WaitForSignal,
    SubProcess
}

public class ProcessNode
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public NodeType Type { get; }
    public List<string> NextNodeIds { get; set; } = new();

    public ProcessNode(NodeType type)
    {
        Type = type;
    }
}

public class NodeExecutionResult
{
    public bool IsCompleted { get; set; }
    public bool RequiresStop { get; set; }
    public string? NextNodeId { get; set; }
    public string? ErrorMessage { get; set; }
}
