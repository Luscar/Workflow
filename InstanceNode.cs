namespace SimpleBPM;

public class InstanceNode
{
    public long NoNoeud { get; set; }
    public long ProcessId { get; set; }
    public string IdNoeud { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public NodeType NodeType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
