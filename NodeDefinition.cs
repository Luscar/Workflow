namespace SimpleBPM;

public enum NodeType
{
    Business,
    Decision,
    Interactive,
    WaitUntilDate,
    WaitForSignal,
    SubProcess,
    End
}

public class NodeDefinition
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public NodeType Type { get; }
    public List<string> NextNodeIds { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, string> ParameterVariableBindings { get; set; } = new();
    public string? OnEnterCommandName { get; set; }
    public Dictionary<string, object> OnEnterCommandParameters { get; set; } = new();
    public Dictionary<string, string> OnEnterCommandParameterVariableBindings { get; set; } = new();

    public NodeDefinition(NodeType type)
    {
        Type = type;
    }

    /// <summary>
    /// Résout les paramètres du nœud en fusionnant les paramètres statiques
    /// et les liaisons de variables de l'instance en cours.
    /// </summary>
    public Dictionary<string, object>? ResolveParameters(Dictionary<string, object> variables)
    {
        if (Parameters.Count == 0 && ParameterVariableBindings.Count == 0)
            return null;

        var result = new Dictionary<string, object>(Parameters);
        foreach (var kvp in ParameterVariableBindings)
        {
            if (variables.TryGetValue(kvp.Value, out var val))
                result[kvp.Key] = val;
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Résout les paramètres de la commande OnEnter en fusionnant les paramètres
    /// statiques et les liaisons de variables de l'instance en cours.
    /// </summary>
    public Dictionary<string, object>? ResolveOnEnterCommandParameters(Dictionary<string, object> variables)
    {
        if (OnEnterCommandParameters.Count == 0 && OnEnterCommandParameterVariableBindings.Count == 0)
            return null;

        var result = new Dictionary<string, object>(OnEnterCommandParameters);
        foreach (var kvp in OnEnterCommandParameterVariableBindings)
        {
            if (variables.TryGetValue(kvp.Value, out var val))
                result[kvp.Key] = val;
        }
        return result.Count > 0 ? result : null;
    }
}

public class NodeExecutionResult
{
    public bool IsCompleted { get; set; }
    public bool RequiresStop { get; set; }
    public string? NextNodeId { get; set; }
    public string? ErrorMessage { get; set; }
}
