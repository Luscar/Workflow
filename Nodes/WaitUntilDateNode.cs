namespace SimpleBPM.Nodes;

public class WaitUntilDateNode : NodeDefinition
{
    public DateTime? TargetDate { get; set; }
    public Func<ProcessInstance, DateTime>? DateProvider { get; set; }
    public string? DateKey { get; set; }

    public WaitUntilDateNode() : base(NodeType.WaitUntilDate)
    {
    }

    public WaitUntilDateNode(DateTime targetDate) : base(NodeType.WaitUntilDate)
    {
        TargetDate = targetDate;
    }

    public WaitUntilDateNode(Func<ProcessInstance, DateTime> dateProvider) : base(NodeType.WaitUntilDate)
    {
        DateProvider = dateProvider;
    }

    public WaitUntilDateNode(string dateKey) : base(NodeType.WaitUntilDate)
    {
        DateKey = dateKey;
    }
}
