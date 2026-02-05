namespace SimpleBPM;

public class ProcessInstance
{
    public long ProcessId { get; set; }
    public long? ParentProcessId { get; set; }
    public string? ParentNodeId { get; set; }
    public string? AggregateId { get; set; }
    public string? DefinitionName { get; set; }
    public string? DefinitionVersion { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object> InternalState { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CurrentNodeId { get; set; }
    public ProcessStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public List<NodeExecutionHistory> ExecutionHistory { get; set; } = new();

    internal SemaphoreSlim ExecutionLock { get; } = new(1, 1);

    public ProcessInstance(long processId, string? aggregateId = null)
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
