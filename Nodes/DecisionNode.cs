namespace SimpleBPM.Nodes;

public class DecisionNode : ProcessNode
{
    public string QueryName { get; set; }
    public Dictionary<string, string> ConditionToNodeId { get; set; } = new();

    public DecisionNode(string queryName) : base(NodeType.Decision)
    {
        QueryName = queryName;
    }

    public override async Task<NodeExecutionResult> ExecuteAsync(ProcessContext context)
    {
        try
        {
            var executor = ProcessEngine.GetCommandQueryExecutor();
            
            if (executor == null)
            {
                return new NodeExecutionResult
                {
                    IsCompleted = false,
                    ErrorMessage = "No command/query executor configured"
                };
            }

            var decisionResult = await executor.ExecuteDecisionAsync(QueryName, context.ProcessId, context.AggregateId);

            if (ConditionToNodeId.TryGetValue(decisionResult, out var nextNodeId))
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

    public DecisionNode AddRoute(string condition, string targetNodeId)
    {
        ConditionToNodeId[condition] = targetNodeId;
        NextNodeIds.Add(targetNodeId);
        return this;
    }
}
