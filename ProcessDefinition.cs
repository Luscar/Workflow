namespace SimpleBPM;

public class ProcessDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Dictionary<string, ProcessNode> Nodes { get; set; } = new();
    public string StartNodeId { get; set; }

    public ProcessDefinition(string name)
    {
        Id = Guid.NewGuid().ToString();
        Name = name;
    }

    public ProcessDefinition AddNode(ProcessNode node)
    {
        Nodes[node.Id] = node;
        
        if (string.IsNullOrEmpty(StartNodeId))
        {
            StartNodeId = node.Id;
        }
        
        return this;
    }

    public ProcessDefinition SetStartNode(string nodeId)
    {
        if (!Nodes.ContainsKey(nodeId))
        {
            throw new ArgumentException($"Node {nodeId} not found in process definition");
        }
        
        StartNodeId = nodeId;
        return this;
    }

    public ProcessNode? GetNode(string nodeId)
    {
        return Nodes.TryGetValue(nodeId, out var node) ? node : null;
    }
}
