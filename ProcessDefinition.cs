namespace SimpleBPM;

public class ProcessDefinition
{
    public string Name { get; set; }
    public string Version { get; set; }
    public Dictionary<string, NodeDefinition> Nodes { get; set; } = new();
    public string StartNodeId { get; set; }

    public ProcessDefinition(string name, string version = "1.0")
    {
        Name = name;
        Version = version;
    }

    public ProcessDefinition AddNode(NodeDefinition node)
    {
        Nodes[node.Name] = node;

        if (string.IsNullOrEmpty(StartNodeId))
        {
            StartNodeId = node.Name;
        }

        return this;
    }

    public ProcessDefinition SetStartNode(string nodeName)
    {
        if (!Nodes.ContainsKey(nodeName))
        {
            throw new ArgumentException($"Node {nodeName} not found in process definition");
        }

        StartNodeId = nodeName;
        return this;
    }

    public NodeDefinition? GetNode(string nodeName)
    {
        return Nodes.TryGetValue(nodeName, out var node) ? node : null;
    }
}
