using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class WaitForSignalNodeHandler : INodeHandler
{
    public NodeType NodeType => NodeType.WaitForSignal;

    public Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var signalNode = (WaitForSignalNode)node;

        instance.Status = ProcessStatus.WaitingSignal;
        instance.CurrentNodeId = node.Id;
        instance.Variables["WaitingForSignal"] = signalNode.SignalName;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = node.NextNodeIds.FirstOrDefault()
        });
    }
}
