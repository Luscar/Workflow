namespace SimpleBPM.Monitor.Models;

public class DashboardDto
{
    public int TotalInstances { get; set; }
    public int RunningCount { get; set; }
    public int WaitingInteractionCount { get; set; }
    public int WaitingSignalCount { get; set; }
    public int WaitingDateCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int DefinitionCount { get; set; }
    public List<StatusBreakdownItem> StatusBreakdown { get; set; } = new();
    public List<DefinitionSummaryDto> DefinitionSummaries { get; set; } = new();
    public List<ProcessInstanceDto> RecentFailed { get; set; } = new();
    public List<ProcessInstanceDto> LongRunning { get; set; } = new();
}

public class StatusBreakdownItem
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class DefinitionSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int ActiveInstances { get; set; }
}
