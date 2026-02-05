using SimpleBPM.Abstractions;

namespace SimpleBPM.Handlers;

public class InteractiveNodeHandler : INodeHandler
{
    private readonly IGestionTache? _gestionTache;

    public NodeType NodeType => NodeType.Interactive;

    public InteractiveNodeHandler(IGestionTache? gestionTache = null)
    {
        _gestionTache = gestionTache;
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        instance.Status = ProcessStatus.WaitingInteraction;
        instance.CurrentNodeId = node.Name;

        if (_gestionTache != null)
        {
            await _gestionTache.CreerTacheAsync(
                instance.ProcessId, instance.AggregateId,
                instance.DefinitionName ?? "", node.DisplayName);
        }

        return new NodeExecutionResult
        {
            IsCompleted = true,
            RequiresStop = true,
            NextNodeId = node.NextNodeIds.FirstOrDefault()
        };
    }

    public async Task OnLeaveAsync(ProcessNode node, ProcessInstance instance)
    {
        if (_gestionTache != null)
        {
            await _gestionTache.FermerTacheAsync(
                instance.ProcessId, instance.AggregateId,
                instance.DefinitionName ?? "", node.DisplayName);
        }
    }
}
