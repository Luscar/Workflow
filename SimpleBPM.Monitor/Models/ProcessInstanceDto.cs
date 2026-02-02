namespace SimpleBPM.Monitor.Models;

public class ProcessInstanceDto
{
    public string Id { get; set; } = string.Empty;
    public string? AggregateId { get; set; }
    public string? DefinitionName { get; set; }
    public string? DefinitionVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? CurrentNodeId { get; set; }
    public string? CurrentNodeName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public string? Duration { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public List<NodeExecutionHistoryDto> ExecutionHistory { get; set; } = new();
    public List<string> PendingSignals { get; set; } = new();
    public Dictionary<string, string> SubProcessIds { get; set; } = new();
}

public class NodeExecutionHistoryDto
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Duration { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NextNodeId { get; set; }
}

public class InstanceListResponse
{
    public List<ProcessInstanceDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
