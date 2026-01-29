namespace SimpleBPM.Nodes;

public class WaitForSignalNode : ProcessNode
{
    public string SignalName { get; set; }

    public WaitForSignalNode(string signalName) : base(NodeType.WaitForSignal)
    {
        SignalName = signalName;
    }

    public override Task<NodeExecutionResult> ExecuteAsync(ProcessInstance instance, Persistence.IProcessRepository? repository = null)
    {
        instance.Status = ProcessStatus.WaitingSignal;
        instance.CurrentNodeId = Id;
        instance.Data["WaitingForSignal"] = SignalName;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = NextNodeIds.FirstOrDefault()
        });
    }
}
