using NSubstitute;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests.Handlers;

public class BusinessNodeHandlerTests
{
    private readonly ICommandQueryExecutor _executor;
    private readonly BusinessNodeHandler _handler;

    public BusinessNodeHandlerTests()
    {
        _executor = Substitute.For<ICommandQueryExecutor>();
        _handler = new BusinessNodeHandler(_executor);
    }

    [Fact]
    public void NodeType_ReturnsBusiness()
    {
        Assert.Equal(NodeType.Business, _handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_Command_ExecutesAndCompletes()
    {
        var node = new BusinessNode("CreateOrder") { Name = "Create Order" };
        node.NextNodeIds.Add("next-node-id");
        var instance = new ProcessInstance("proc-1", "agg-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("next-node-id", result.NextNodeId);
        await _executor.Received(1).ExecuteAsync("CreateOrder", "proc-1", "agg-1", false);
    }

    [Fact]
    public async Task HandleAsync_Query_ExecutesAsQuery()
    {
        var node = new BusinessNode("GetStock", isQuery: true) { Name = "Get Stock" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("proc-1", "agg-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        await _executor.Received(1).ExecuteAsync("GetStock", "proc-1", "agg-1", true);
    }

    [Fact]
    public async Task HandleAsync_NoNextNode_ReturnsNullNextNodeId()
    {
        var node = new BusinessNode("FinalStep") { Name = "Final" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.Null(result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_ExecutorThrows_ReturnsFailed()
    {
        var node = new BusinessNode("FailingCommand") { Name = "Fail" };
        var instance = new ProcessInstance("proc-1");
        _executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromException(new InvalidOperationException("DB error")));

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("DB error", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_NullAggregateId_PassesNull()
    {
        var node = new BusinessNode("SomeCommand") { Name = "Cmd" };
        var instance = new ProcessInstance("proc-1");

        await _handler.HandleAsync(node, instance);

        await _executor.Received(1).ExecuteAsync("SomeCommand", "proc-1", null, false);
    }

    [Fact]
    public void Constructor_NullExecutor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessNodeHandler(null!));
    }
}
