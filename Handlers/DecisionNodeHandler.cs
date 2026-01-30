using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class DecisionNodeHandler : INodeHandler
{
    private readonly ICommandExecutor _executor;

    public NodeType NodeType => NodeType.Decision;

    public DecisionNodeHandler(ICommandExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var decisionNode = (DecisionNode)node;

        try
        {
            var decisionResult = await _executor.EvaluateDecisionAsync(decisionNode.QueryName, instance.ProcessId, instance.AggregateId);

            if (decisionNode.ConditionToNodeId.TryGetValue(decisionResult, out var nextNodeId))
            {
                return new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = nextNodeId
                };
            }

            return new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = $"No route found for decision result: {decisionResult}"
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
