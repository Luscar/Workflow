namespace SimpleBPM.Monitor.Models;

public class ProcessDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string StartNodeId { get; set; } = string.Empty;
    public List<ProcessNodeDto> Nodes { get; set; } = new();
    public List<NodeConnectionDto> Connections { get; set; } = new();
}

public class ProcessNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsStartNode { get; set; }
    public List<string> NextNodeIds { get; set; } = new();
    public Dictionary<string, object>? Metadata { get; set; }
}

public class NodeConnectionDto
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string? Label { get; set; }
}
