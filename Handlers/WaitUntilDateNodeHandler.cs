using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class WaitUntilDateNodeHandler : INodeHandler
{
    private readonly IBpmMediateur? _executor;

    public NodeType NodeType => NodeType.WaitUntilDate;

    public WaitUntilDateNodeHandler(IBpmMediateur? executor = null)
    {
        _executor = executor;
    }

    public async Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance)
    {
        var waitNode = (WaitUntilDateNode)node;

        DateTime? targetDate = null;

        // Lire la date depuis la banque de l'instance (InternalState) si elle a déjà été résolue
        if (instance.InternalState.TryGetValue("WaitUntilDate", out var storedDate) && storedDate is DateTime storedDateTime)
        {
            targetDate = storedDateTime;
        }
        else
        {
            // Résoudre la date depuis la définition du noeud
            targetDate = waitNode.TargetDate ?? waitNode.DateProvider?.Invoke(instance);

            // Essayer de récupérer la date depuis les variables via DateKey
            if (targetDate == null && !string.IsNullOrEmpty(waitNode.DateKey) && instance.Variables.TryGetValue(waitNode.DateKey, out var dateValue))
            {
                targetDate = dateValue switch
                {
                    DateTime dt => dt,
                    string s when DateTime.TryParse(s, out var parsed) => parsed,
                    _ => null
                };
            }

            // Stocker la date résolue dans la banque de l'instance pour les reprises futures
            if (targetDate != null)
            {
                instance.InternalState["WaitUntilDate"] = targetDate.Value;
            }
        }

        if (targetDate == null)
        {
            return new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = "Aucune date cible configurée"
            };
        }

        if (DateTime.UtcNow >= targetDate.Value)
        {
            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeName = node.NextNodeIds.FirstOrDefault()
            };
        }

        instance.Status = ProcessStatus.WaitingDate;
        instance.CurrentNodeName = node.Name;

        if (_executor != null && !string.IsNullOrEmpty(node.OnEnterCommandName))
        {
            try
            {
                var parameters = node.OnEnterCommandParameters.Count > 0 ? node.OnEnterCommandParameters : null;
                await _executor.ExecuteCommandAsync(node.OnEnterCommandName, instance.ProcessId, instance.AggregateId, parameters);
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

        return new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeName = node.NextNodeIds.FirstOrDefault()
        };
    }
}
