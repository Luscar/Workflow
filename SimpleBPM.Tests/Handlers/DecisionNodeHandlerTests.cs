using NSubstitute;
using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests.Handlers;

public class DecisionNodeHandlerTests
{
    private readonly ICommandExecutor _executor;
    private readonly DecisionNodeHandler _handler;

    public DecisionNodeHandlerTests()
    {
        _executor = Substitute.For<ICommandExecutor>();
        _handler = new DecisionNodeHandler(_executor);
    }

    [Fact]
    public void NodeType_ReturnsDecision()
    {
        Assert.Equal(NodeType.Decision, _handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_MatchingRoute_ReturnsRouteTarget()
    {
        var node = new DecisionNode("CheckApproval") { Name = "Check" };
        node.AddRoute("approved", "approve-node-id");
        node.AddRoute("rejected", "reject-node-id");
        var instance = new ProcessInstance("proc-1", "agg-1");

        _executor.EvaluateDecisionAsync("CheckApproval", "proc-1", "agg-1")
            .Returns("approved");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("approve-node-id", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_SecondRoute_ReturnsCorrectTarget()
    {
        var node = new DecisionNode("CheckStatus") { Name = "Status" };
        node.AddRoute("active", "active-node");
        node.AddRoute("inactive", "inactive-node");
        var instance = new ProcessInstance("proc-1");

        _executor.EvaluateDecisionAsync("CheckStatus", "proc-1", null)
            .Returns("inactive");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.Equal("inactive-node", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingRoute_ReturnsFailed()
    {
        var node = new DecisionNode("CheckSomething") { Name = "Check" };
        node.AddRoute("yes", "yes-node");
        node.AddRoute("no", "no-node");
        var instance = new ProcessInstance("proc-1");

        _executor.EvaluateDecisionAsync("CheckSomething", "proc-1", null)
            .Returns("maybe");

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No route found", result.ErrorMessage);
        Assert.Contains("maybe", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ExecutorThrows_ReturnsFailed()
    {
        var node = new DecisionNode("FailQuery") { Name = "Fail" };
        node.AddRoute("ok", "ok-node");
        var instance = new ProcessInstance("proc-1");

        _executor.EvaluateDecisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException<string>(new Exception("Query failed")));

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Query failed", result.ErrorMessage);
    }

    [Fact]
    public void Constructor_NullExecutor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DecisionNodeHandler(null!));
    }
}
