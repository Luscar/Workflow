namespace SimpleBPM.Nodes;

public class DecisionNode : NodeDefinition
{
    public string? QueryName { get; set; }
    public Dictionary<string, string> ConditionToNodeId { get; set; } = new();
    public List<ConditionDecision> Conditions { get; set; } = new();
    public string? NoeudParDefaut { get; set; }

    public DecisionNode() : base(NodeType.Decision) { }

    public DecisionNode(string queryName) : base(NodeType.Decision)
    {
        QueryName = queryName;
    }

    public DecisionNode AddRoute(string condition, string targetNodeId)
    {
        ConditionToNodeId[condition] = targetNodeId;
        NextNodeIds.Add(targetNodeId);
        return this;
    }

    public DecisionNode AddCondition(string nomVariable, object? valeur, OperateurFiltre operateur, TypeDonnee typeDonnee, string noeudCible)
    {
        Conditions.Add(new ConditionDecision(nomVariable, valeur, operateur, typeDonnee, noeudCible));
        if (!NextNodeIds.Contains(noeudCible))
            NextNodeIds.Add(noeudCible);
        return this;
    }

    public DecisionNode SetNoeudParDefaut(string noeudId)
    {
        NoeudParDefaut = noeudId;
        if (!NextNodeIds.Contains(noeudId))
            NextNodeIds.Add(noeudId);
        return this;
    }
}
