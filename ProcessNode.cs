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

public abstract class ProcessNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public NodeType Type { get; }
    public List<string> NextNodeIds { get; set; } = new();

    protected ProcessNode(NodeType type)
    {
        Type = type;
        Id = Guid.NewGuid().ToString();
    }

    public abstract Task<NodeExecutionResult> ExecuteAsync(ProcessContext context, Persistence.IProcessRepository? repository = null);
}

public class NodeExecutionResult
{
    public bool IsCompleted { get; set; }
    public bool RequiresStop { get; set; }
    public string? NextNodeId { get; set; }
    public string? ErrorMessage { get; set; }
}
