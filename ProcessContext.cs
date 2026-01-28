namespace SimpleBPM;

public class ProcessContext
{
    public string ProcessId { get; set; }
    public string? AggregateId { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CurrentNodeId { get; set; }
    public ProcessStatus Status { get; set; }
    public List<NodeExecutionHistory> ExecutionHistory { get; set; } = new();

    public ProcessContext(string processId, string? aggregateId = null)
    {
        ProcessId = processId;
        AggregateId = aggregateId;
        StartedAt = DateTime.UtcNow;
        Status = ProcessStatus.Running;
    }

    public TimeSpan? TotalDuration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    
    public TimeSpan CurrentDuration => DateTime.UtcNow - StartedAt;

    public int CompletedStepsCount => ExecutionHistory.Count(h => h.Success);
    
    public int FailedStepsCount => ExecutionHistory.Count(h => !h.Success);
}

public enum ProcessStatus
{
    Running,
    WaitingInteraction,
    WaitingDate,
    WaitingSignal,
    Completed,
    Failed
}
