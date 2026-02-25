namespace SimpleBPM.Handlers;

public class EndNodeHandler : INodeHandler
{
    public NodeType NodeType => NodeType.End;

    public Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance)
    {
        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = false,
            NextNodeName = null
        });
    }
}
