namespace SimpleBPM;

/// <summary>
/// Raised when a subprocess completes execution, carrying the output data
/// back toward the parent process.
/// </summary>
public class SubProcessCompletedEvent
{
    public string SubProcessId { get; set; } = string.Empty;
    public string ParentProcessId { get; set; } = string.Empty;
    public string ParentNodeId { get; set; } = string.Empty;
    public Dictionary<string, object> OutputData { get; set; } = new();
    public ProcessStatus SubProcessStatus { get; set; }
    public DateTime CompletedAt { get; set; }
}
