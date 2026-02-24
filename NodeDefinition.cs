namespace SimpleBPM;

public enum NodeType
{
    Business,
    Decision,
    Interactive,
    WaitUntilDate,
    WaitForSignal,
    SubProcess,
    End
}

public class NodeDefinition
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public NodeType Type { get; }
    public List<string> NextNodeIds { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? OnEnterCommandName { get; set; }

    public NodeDefinition(NodeType type)
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
