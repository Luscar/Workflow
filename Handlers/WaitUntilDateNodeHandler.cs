using SimpleBPM.Nodes;

namespace SimpleBPM.Handlers;

public class WaitUntilDateNodeHandler : INodeHandler
{
    public NodeType NodeType => NodeType.WaitUntilDate;

    public Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
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
            return Task.FromResult(new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = "No target date configured"
            });
        }

        if (DateTime.UtcNow >= targetDate.Value)
        {
            return Task.FromResult(new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = node.NextNodeIds.FirstOrDefault()
            });
        }

        instance.Status = ProcessStatus.WaitingDate;
        instance.CurrentNodeId = node.Id;
        instance.Variables["WaitUntilDate"] = targetDate.Value;

        return Task.FromResult(new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = node.NextNodeIds.FirstOrDefault()
        });
    }
}
