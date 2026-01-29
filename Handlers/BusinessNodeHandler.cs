using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class BusinessNodeHandler : INodeHandler
{
    private readonly ICommandQueryExecutor _executor;

    public NodeType NodeType => NodeType.Business;

    public BusinessNodeHandler(ICommandQueryExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var businessNode = (BusinessNode)node;

        try
        {
            await _executor.ExecuteAsync(businessNode.CommandOrQueryName, instance.ProcessId, instance.AggregateId, businessNode.IsQuery);

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
