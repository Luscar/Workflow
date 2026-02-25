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

        DateTime? targetDate = waitNode.TargetDate ?? waitNode.DateProvider?.Invoke(instance);

        // Essayer de récupérer la date depuis le contexte via DateKey
        if (targetDate == null && !string.IsNullOrEmpty(waitNode.DateKey) && instance.Variables.TryGetValue(waitNode.DateKey, out var dateValue))
        {
            targetDate = dateValue switch
            {
                DateTime dt => dt,
                string s when DateTime.TryParse(s, out var parsed) => parsed,
                _ => null
            };
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
        instance.InternalState["WaitUntilDate"] = targetDate.Value;

        if (_executor != null && !string.IsNullOrEmpty(node.OnEnterCommandName))
        {
            try
            {
                await _executor.ExecuteCommandAsync(node.OnEnterCommandName, instance.ProcessId, instance.AggregateId);
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
