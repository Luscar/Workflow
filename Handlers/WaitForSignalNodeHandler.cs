using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class WaitForSignalNodeHandler : INodeHandler
{
    private readonly ICommandExecutor? _executor;

    public NodeType NodeType => NodeType.WaitForSignal;

    public WaitForSignalNodeHandler(ICommandExecutor? executor = null)
    {
        _executor = executor;
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var signalNode = (WaitForSignalNode)node;

        instance.Status = ProcessStatus.WaitingSignal;
        instance.CurrentNodeId = node.Name;
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
            NextNodeId = node.NextNodeIds.FirstOrDefault()
        };
    }
}
