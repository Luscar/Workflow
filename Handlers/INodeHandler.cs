namespace SimpleBPM.Handlers;

public interface INodeHandler
{
    NodeType NodeType { get; }
    Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance);
    Task OnLeaveAsync(NodeDefinition node, ProcessInstance instance) => Task.CompletedTask;
}
