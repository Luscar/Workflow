namespace SimpleBPM.Handlers;

public class InteractiveNodeHandler : INodeHandler
{
    public NodeType NodeType => NodeType.Interactive;

    public Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        instance.Status = ProcessStatus.WaitingInteraction;
        instance.CurrentNodeId = node.Id;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = node.NextNodeIds.FirstOrDefault()
        });
    }
}
