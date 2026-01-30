namespace SimpleBPM.Handlers;

public interface INodeHandler
{
    NodeType NodeType { get; }
    Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance);
    Task OnLeaveAsync(ProcessNode node, ProcessInstance instance) => Task.CompletedTask;
}
