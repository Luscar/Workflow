namespace SimpleBPM.Migration;

public class ProcessMigration
{
    public string FromVersion { get; }
    public string ToVersion { get; }

    private readonly Dictionary<string, string> _nodeNameMap = new();
    private readonly List<Action<Dictionary<string, object>>> _variableTransforms = new();

    public ProcessMigration(string fromVersion, string toVersion)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }

    /// <summary>
    /// Map a node name from the source definition to a node name in the target definition.
    /// Nodes with the same name across versions are mapped automatically.
    /// </summary>
    public ProcessMigration MapNode(string fromNodeName, string toNodeName)
    {
        _nodeNameMap[fromNodeName] = toNodeName;
        return this;
    }

    /// <summary>
    /// Add a variable transformation to apply during migration.
    /// Transforms execute in the order they are added.
    /// </summary>
    public ProcessMigration TransformVariables(Action<Dictionary<string, object>> transform)
    {
        _variableTransforms.Add(transform);
        return this;
    }

    internal string ResolveNodeName(string currentNodeName)
    {
        return _nodeNameMap.TryGetValue(currentNodeName, out var mapped) ? mapped : currentNodeName;
    }

    internal void ApplyVariableTransforms(Dictionary<string, object> variables)
    {
        foreach (var transform in _variableTransforms)
            transform(variables);
    }
}
