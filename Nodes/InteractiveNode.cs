namespace SimpleBPM.Nodes;

public class InteractiveNode : ProcessNode
{
    public InteractiveNode() : base(NodeType.Interactive)
    {
    }

    public override Task<NodeExecutionResult> ExecuteAsync(ProcessInstance instance, Persistence.IProcessRepository? repository = null)
    {
        instance.Status = ProcessStatus.WaitingInteraction;
        instance.CurrentNodeId = Id;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = NextNodeIds.FirstOrDefault()
        });
    }
}
