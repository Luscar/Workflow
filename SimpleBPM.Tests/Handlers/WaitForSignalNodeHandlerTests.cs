using SimpleBPM.Handlers;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests.Handlers;

public class WaitForSignalNodeHandlerTests
{
    private readonly WaitForSignalNodeHandler _handler = new();

    [Fact]
    public void NodeType_ReturnsWaitForSignal()
    {
        Assert.Equal(NodeType.WaitForSignal, _handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_SetsWaitingSignalStatus()
    {
        var node = new WaitForSignalNode("PaymentReceived") { Name = "WaitPayment" };
        var instance = new ProcessInstance("proc-1");

        await _handler.HandleAsync(node, instance);

        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);
    }

    [Fact]
    public async Task HandleAsync_SetsCurrentNodeId()
    {
        var node = new WaitForSignalNode("PaymentReceived") { Name = "WaitPayment" };
        var instance = new ProcessInstance("proc-1");

        await _handler.HandleAsync(node, instance);

        Assert.Equal(node.Id, instance.CurrentNodeId);
    }

    [Fact]
    public async Task HandleAsync_StoresSignalNameInVariables()
    {
        var node = new WaitForSignalNode("OrderShipped") { Name = "WaitShipment" };
        var instance = new ProcessInstance("proc-1");

        await _handler.HandleAsync(node, instance);

        Assert.Equal("OrderShipped", instance.Variables["WaitingForSignal"]);
    }

    [Fact]
    public async Task HandleAsync_RequiresStop()
    {
        var node = new WaitForSignalNode("SomeSignal") { Name = "Wait" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_WithNextNode_ReturnsNextNodeId()
    {
        var node = new WaitForSignalNode("Approval") { Name = "WaitApproval" };
        node.NextNodeIds.Add("after-signal");
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.Equal("after-signal", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_NoNextNode_ReturnsNullNextNodeId()
    {
        var node = new WaitForSignalNode("FinalSignal") { Name = "LastWait" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.Null(result.NextNodeId);
    }
}
