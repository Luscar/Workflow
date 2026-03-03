namespace SimpleBPM;

public class NodeInstance
{
    public long NoSeqNoeud { get; set; }
    public string NodeId { get; set; }
    public NodeType NodeType { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NextNodeId { get; set; }

    public NodeInstance(string nodeId, NodeType nodeType)
    {
        NodeId = nodeId;
        NodeType = nodeType;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(bool success, string? errorMessage = null, string? nextNodeId = null)
    {
        CompletedAt = DateTime.UtcNow;
        Success = success;
        ErrorMessage = errorMessage;
        NextNodeId = nextNodeId;
    }
}
