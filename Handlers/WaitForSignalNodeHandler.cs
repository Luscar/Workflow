using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class WaitForSignalNodeHandler : INodeHandler
{
    private readonly IBpmMediateur? _executor;

    public NodeType NodeType => NodeType.WaitForSignal;

    public WaitForSignalNodeHandler(IBpmMediateur? executor = null)
    {
        _executor = executor;
    }

    public async Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance)
    {
        var signalNode = (WaitForSignalNode)node;

        instance.Status = ProcessStatus.WaitingSignal;
        instance.CurrentNodeName = node.Name;
        instance.InternalState["WaitingForSignal"] = signalNode.SignalName;

        if (_executor != null && !string.IsNullOrEmpty(node.OnEnterCommandName))
        {
            try
            {
                await _executor.ExecuteCommandAsync(node.OnEnterCommandName, instance.ProcessId, instance.AggregateId);
            }
            catch (Exception ex)
            {
                return new NodeExecutionResult
                {
                    IsCompleted = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        return new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeName = node.NextNodeIds.FirstOrDefault()
        };
    }
}
