using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class BusinessNodeHandler : INodeHandler
{
    private readonly IBpmMediateur _executor;

    public NodeType NodeType => NodeType.Business;

    public BusinessNodeHandler(IBpmMediateur executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance)
    {
        var businessNode = (BusinessNode)node;

        try
        {
            var parameters = businessNode.ResolveParameters(instance.Variables);
            await _executor.ExecuteCommandAsync(businessNode.CommandName, instance.ProcessId, instance.AggregateId, parameters);

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
