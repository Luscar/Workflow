namespace SimpleBPM.Nodes;

public class WaitUntilDateNode : ProcessNode
{
    public DateTime? TargetDate { get; set; }
    public Func<ProcessContext, DateTime>? DateProvider { get; set; }

    public WaitUntilDateNode() : base(NodeType.WaitUntilDate)
    {
    }

    public WaitUntilDateNode(DateTime targetDate) : base(NodeType.WaitUntilDate)
    {
        TargetDate = targetDate;
    }

    public WaitUntilDateNode(Func<ProcessContext, DateTime> dateProvider) : base(NodeType.WaitUntilDate)
    {
        DateProvider = dateProvider;
    }

    public override Task<NodeExecutionResult> ExecuteAsync(ProcessContext context)
    {
        var targetDate = TargetDate ?? DateProvider?.Invoke(context);

        if (targetDate == null)
        {
            return Task.FromResult(new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = "No target date configured"
            });
        }

        if (DateTime.UtcNow >= targetDate.Value)
        {
            return Task.FromResult(new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = NextNodeIds.FirstOrDefault()
            });
        }

        context.Status = ProcessStatus.WaitingDate;
        context.CurrentNodeId = Id;
        context.Data["WaitUntilDate"] = targetDate.Value;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = NextNodeIds.FirstOrDefault()
        });
    }
}
