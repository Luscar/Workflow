namespace SimpleBPM.Nodes;

public class WaitUntilDateNode : ProcessNode
{
    public DateTime? TargetDate { get; set; }
    public Func<ProcessContext, DateTime>? DateProvider { get; set; }
    public string? DateKey { get; set; }

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

    /// <summary>
    /// Constructeur avec clé de date - récupère la date depuis context.Data[dateKey]
    /// </summary>
    public WaitUntilDateNode(string dateKey) : base(NodeType.WaitUntilDate)
    {
        DateKey = dateKey;
    }

    public override Task<NodeExecutionResult> ExecuteAsync(ProcessContext context, Persistence.IProcessRepository? repository = null)
    {
        DateTime? targetDate = TargetDate ?? DateProvider?.Invoke(context);

        // Essayer de récupérer la date depuis le contexte via DateKey
        if (targetDate == null && !string.IsNullOrEmpty(DateKey) && context.Data.TryGetValue(DateKey, out var dateValue))
        {
            targetDate = dateValue switch
            {
                DateTime dt => dt,
                string s when DateTime.TryParse(s, out var parsed) => parsed,
                _ => null
            };
        }

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
