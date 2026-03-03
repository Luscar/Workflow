namespace SimpleBPM.Migration;

public class ProcessMigration
{
    public string FromVersion { get; set; }
    public string ToVersion { get; set; }
    public Dictionary<string, string> NodeMappings { get; set; } = new();
    public List<VariableTransform> VariableTransforms { get; set; } = new();

    public ProcessMigration(string fromVersion, string toVersion)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }

    public ProcessMigration MapNode(string fromNodeId, string toNodeId)
    {
        NodeMappings[fromNodeId] = toNodeId;
        return this;
    }

    public ProcessMigration SetVariable(string name, object value)
    {
        VariableTransforms.Add(VariableTransform.Set(name, value));
        return this;
    }

    public ProcessMigration RenameVariable(string from, string to)
    {
        VariableTransforms.Add(VariableTransform.Rename(from, to));
        return this;
    }

    public ProcessMigration RemoveVariable(string name)
    {
        VariableTransforms.Add(VariableTransform.Remove(name));
        return this;
    }

    internal string ResolveNodeId(string currentNodeId)
    {
        return NodeMappings.TryGetValue(currentNodeId, out var mapped) ? mapped : currentNodeId;
    }

    internal void ApplyVariableTransforms(Dictionary<string, object> variables)
    {
        foreach (var transform in VariableTransforms)
            transform.Apply(variables);
    }
}
