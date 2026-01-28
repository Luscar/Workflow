namespace SimpleBPM.Nodes;

public class WaitForSignalNode : ProcessNode
{
    public string SignalName { get; set; }

    public WaitForSignalNode(string signalName) : base(NodeType.WaitForSignal)
    {
        SignalName = signalName;
    }

    public override Task<NodeExecutionResult> ExecuteAsync(ProcessContext context, Persistence.IProcessRepository? repository = null)
    {
        context.Status = ProcessStatus.WaitingSignal;
        context.CurrentNodeId = Id;
        context.Data["WaitingForSignal"] = SignalName;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = NextNodeIds.FirstOrDefault()
        });
    }
}
