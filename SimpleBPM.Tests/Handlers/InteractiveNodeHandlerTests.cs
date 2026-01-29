using SimpleBPM.Handlers;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests.Handlers;

public class InteractiveNodeHandlerTests
{
    private readonly InteractiveNodeHandler _handler = new();

    [Fact]
    public void NodeType_ReturnsInteractive()
    {
        Assert.Equal(NodeType.Interactive, _handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_SetsWaitingInteractionStatus()
    {
        var node = new InteractiveNode { Name = "UserApproval" };
        var instance = new ProcessInstance("proc-1");

        await _handler.HandleAsync(node, instance);

        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);
    }

    [Fact]
    public async Task HandleAsync_SetsCurrentNodeId()
    {
        var node = new InteractiveNode { Name = "UserApproval" };
        var instance = new ProcessInstance("proc-1");

        await _handler.HandleAsync(node, instance);

        Assert.Equal(node.Id, instance.CurrentNodeId);
    }

    [Fact]
    public async Task HandleAsync_RequiresStop()
    {
        var node = new InteractiveNode { Name = "UserApproval" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_WithNextNode_ReturnsNextNodeId()
    {
        var node = new InteractiveNode { Name = "UserApproval" };
        node.NextNodeIds.Add("next-step");
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.Equal("next-step", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_NoNextNode_ReturnsNullNextNodeId()
    {
        var node = new InteractiveNode { Name = "FinalApproval" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.Null(result.NextNodeId);
    }
}
