namespace SimpleBPM.Nodes;

public class InteractiveNode : ProcessNode
{
    public InteractiveNode() : base(NodeType.Interactive)
    {
    }

    public override Task<NodeExecutionResult> ExecuteAsync(ProcessContext context, Persistence.IProcessRepository? repository = null)
    {
        context.Status = ProcessStatus.WaitingInteraction;
        context.CurrentNodeId = Id;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = NextNodeIds.FirstOrDefault()
        });
    }
}
