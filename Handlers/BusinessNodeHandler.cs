using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class BusinessNodeHandler : INodeHandler
{
    private readonly ICommandExecutor _executor;

    public NodeType NodeType => NodeType.Business;

    public BusinessNodeHandler(ICommandExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var businessNode = (BusinessNode)node;

        try
        {
            await _executor.ExecuteCommandAsync(businessNode.CommandName, instance.ProcessId, instance.AggregateId);

            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = node.NextNodeIds.FirstOrDefault()
            };
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
}
