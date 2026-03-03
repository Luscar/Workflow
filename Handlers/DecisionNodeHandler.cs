using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class DecisionNodeHandler : INodeHandler
{
    private readonly IBpmMediateur? _executor;

    public NodeType NodeType => NodeType.Decision;

    public DecisionNodeHandler() { }

    public DecisionNodeHandler(IBpmMediateur executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance)
    {
        var decisionNode = (DecisionNode)node;

        try
        {
            if (decisionNode.Conditions.Count > 0)
            {
                return EvaluerConditions(decisionNode, instance);
            }

            return await EvaluerParQuery(decisionNode, instance);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private NodeExecutionResult EvaluerConditions(DecisionNode decisionNode, ProcessInstance instance)
    {
        foreach (var condition in decisionNode.Conditions)
        {
            if (condition.Evaluer(instance.Variables))
            {
                return new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = condition.NoeudCible
                };
            }
        }

        if (!string.IsNullOrEmpty(decisionNode.NoeudParDefaut))
        {
            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = decisionNode.NoeudParDefaut
            };
        }

        return new NodeExecutionResult
        {
            IsCompleted = false,
            ErrorMessage = "Aucune condition ne correspond et aucun nœud par défaut n'est défini"
        };
    }

    private async Task<NodeExecutionResult> EvaluerParQuery(DecisionNode decisionNode, ProcessInstance instance)
    {
        if (_executor == null)
        {
            return new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = "Aucun executor configuré pour évaluer la requête de décision"
            };
        }

        var parameters = decisionNode.Parameters.Count > 0 ? decisionNode.Parameters : null;
        var decisionResult = await _executor.EvaluateDecisionAsync(decisionNode.QueryName!, instance.ProcessId, instance.AggregateId, parameters);

        if (decisionNode.ConditionToNodeId.TryGetValue(decisionResult, out var nextNodeId))
        {
            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = nextNodeId
            };
        }

        return new NodeExecutionResult
        {
            IsCompleted = false,
            ErrorMessage = $"Aucune route trouvée pour le résultat de décision : {decisionResult}"
        };
    }
}
