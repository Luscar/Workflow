namespace SimpleBPM;

public class NoeudProcessus
{
    public long NoSeqNoeud { get; set; }
    public long ProcessId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public NodeType NodeType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
