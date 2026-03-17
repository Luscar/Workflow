namespace SimpleBPM.Nodes;

public class WaitUntilDateNode : NodeDefinition
{
    public DateTime? TargetDate { get; set; }
    public Func<ProcessInstance, DateTime>? DateProvider { get; set; }
    public string? DateKey { get; set; }
    public string? DateQueryName { get; set; }
    public Dictionary<string, object> DateQueryParameters { get; set; } = new();
    public Dictionary<string, string> DateQueryParameterVariableBindings { get; set; } = new();

    /// <summary>
    /// Résout les paramètres de la query de date en fusionnant les paramètres statiques
    /// et les liaisons de variables de l'instance en cours.
    /// </summary>
    public Dictionary<string, object>? ResolveDateQueryParameters(Dictionary<string, object> variables)
    {
        if (DateQueryParameters.Count == 0 && DateQueryParameterVariableBindings.Count == 0)
            return null;

        var result = new Dictionary<string, object>(DateQueryParameters);
        foreach (var kvp in DateQueryParameterVariableBindings)
        {
            if (variables.TryGetValue(kvp.Value, out var val))
                result[kvp.Key] = val;
        }
        return result.Count > 0 ? result : null;
    }

    public WaitUntilDateNode() : base(NodeType.WaitUntilDate)
    {
    }

    public WaitUntilDateNode(DateTime targetDate) : base(NodeType.WaitUntilDate)
    {
        TargetDate = targetDate;
    }

    public WaitUntilDateNode(Func<ProcessInstance, DateTime> dateProvider) : base(NodeType.WaitUntilDate)
    {
        DateProvider = dateProvider;
    }

    public WaitUntilDateNode(string dateKey) : base(NodeType.WaitUntilDate)
    {
        DateKey = dateKey;
    }
}
