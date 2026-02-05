namespace SimpleBPM;

public class ProcessDefinition
{
    public string Name { get; set; }
    public string Version { get; set; }
    public Dictionary<string, ProcessNode> Nodes { get; set; } = new();
    public string StartNodeId { get; set; }

    public ProcessDefinition(string name, string version = "1.0")
    {
        Name = name;
        Version = version;
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

    public ProcessNode? GetNodeByName(string name)
    {
        return Nodes.Values.FirstOrDefault(n => n.Name == name);
    }
}
