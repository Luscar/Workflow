namespace SimpleBPM.Nodes;

public class BusinessNode : ProcessNode
{
    public string CommandOrQueryName { get; set; }
    public bool IsQuery { get; set; }

    public BusinessNode(string commandOrQueryName, bool isQuery = false) : base(NodeType.Business)
    {
        CommandOrQueryName = commandOrQueryName;
        IsQuery = isQuery;
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

            await executor.ExecuteAsync(CommandOrQueryName, context.ProcessId, context.AggregateId, IsQuery);

            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = NextNodeIds.FirstOrDefault()
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
