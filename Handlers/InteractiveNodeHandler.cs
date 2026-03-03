using SimpleBPM.Abstractions;

namespace SimpleBPM.Handlers;

public class InteractiveNodeHandler : INodeHandler
{
    private readonly IGestionTache? _gestionTache;
    private readonly IBpmMediateur? _executor;

    public NodeType NodeType => NodeType.Interactive;

    public InteractiveNodeHandler(IGestionTache? gestionTache = null, IBpmMediateur? executor = null)
    {
        _gestionTache = gestionTache;
        _executor = executor;
    }

    public async Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance)
    {
        instance.Status = ProcessStatus.WaitingInteraction;
        instance.CurrentNodeId = node.Name;

        if (_gestionTache != null)
        {
            await _gestionTache.CreerTacheAsync(
                instance.ProcessId, instance.AggregateId,
                instance.DefinitionName ?? "", node.DisplayName);
        }

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
            NextNodeId = node.NextNodeIds.FirstOrDefault()
        };
    }

    public async Task OnLeaveAsync(NodeDefinition node, ProcessInstance instance)
    {
        if (_gestionTache != null)
        {
            await _gestionTache.FermerTacheAsync(
                instance.ProcessId, instance.AggregateId,
                instance.DefinitionName ?? "", node.DisplayName);
        }
    }
}
