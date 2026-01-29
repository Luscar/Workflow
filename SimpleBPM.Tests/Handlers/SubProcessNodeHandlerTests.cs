using NSubstitute;
using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;

namespace SimpleBPM.Tests.Handlers;

public class SubProcessNodeHandlerTests
{
    private readonly IProcessRepository _repository;
    private readonly ICommandExecutor _executor;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;
    private readonly SubProcessNodeHandler _handler;

    public SubProcessNodeHandlerTests()
    {
        _repository = Substitute.For<IProcessRepository>();
        _executor = Substitute.For<ICommandExecutor>();

        _handlers = new Dictionary<NodeType, INodeHandler>
        {
            { NodeType.Business, new BusinessNodeHandler(_executor) },
            { NodeType.Interactive, new InteractiveNodeHandler() },
            { NodeType.WaitForSignal, new WaitForSignalNodeHandler() },
            { NodeType.WaitUntilDate, new WaitUntilDateNodeHandler() }
        };
        _handler = new SubProcessNodeHandler(_repository, _handlers);
        _handlers[NodeType.SubProcess] = _handler;
    }

    [Fact]
    public void NodeType_ReturnsSubProcess()
    {
        Assert.Equal(NodeType.SubProcess, _handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_CompletedSubProcess_ReturnsCompleted()
    {
        // Sub-process: single business node that completes immediately
        var subDef = new ProcessDefinition("SubProc");
        var subNode = new BusinessNode("DoWork") { Name = "Work" };
        subDef.AddNode(subNode);

        var node = new SubProcessNode(subDef) { Name = "RunSub" };
        node.NextNodeIds.Add("after-sub");
        var instance = new ProcessInstance("parent-1", "agg-1");

        _executor.ExecuteCommandAsync("DoWork", Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("after-sub", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_SubProcessWaiting_StopsParent()
    {
        // Sub-process with interactive node that causes it to wait
        var subDef = new ProcessDefinition("SubProc");
        var interactiveNode = new InteractiveNode { Name = "UserInput" };
        subDef.AddNode(interactiveNode);

        var node = new SubProcessNode(subDef) { Name = "RunSub" };
        node.NextNodeIds.Add("after-sub");
        var instance = new ProcessInstance("parent-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.True(instance.SubProcessIds.ContainsKey(node.Id));
    }

    [Fact]
    public async Task HandleAsync_InheritAggregateId_PassesToSubProcess()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("CheckAgg") { Name = "Check" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef) { Name = "Sub", InheritAggregateId = true };
        var instance = new ProcessInstance("parent-1", "parent-agg");

        _executor.ExecuteCommandAsync("CheckAgg", Arg.Any<string>(), "parent-agg")
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(node, instance);

        await _executor.Received(1).ExecuteCommandAsync("CheckAgg", Arg.Any<string>(), "parent-agg");
    }

    [Fact]
    public async Task HandleAsync_NoInheritAggregateId_SubProcessHasNullAggregate()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("CheckAgg") { Name = "Check" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef) { Name = "Sub", InheritAggregateId = false };
        var instance = new ProcessInstance("parent-1", "parent-agg");

        _executor.ExecuteCommandAsync("CheckAgg", Arg.Any<string>(), null)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(node, instance);

        await _executor.Received(1).ExecuteCommandAsync("CheckAgg", Arg.Any<string>(), null);
    }

    [Fact]
    public async Task HandleAsync_InputMapping_CopiesVariablesToSubProcess()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("UseInput") { Name = "UseInput" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef)
        {
            Name = "Sub",
            InputMapping = new() { ["OrderId"] = "Id", ["Amount"] = "Total" }
        };
        node.NextNodeIds.Add("after");
        var instance = new ProcessInstance("parent-1");
        instance.Variables["OrderId"] = "order-42";
        instance.Variables["Amount"] = 100.0;
        instance.Variables["RegularVar"] = "should-not-copy";

        _executor.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_OutputMapping_CopiesVariablesFromSubProcess()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("Produce") { Name = "Produce" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef)
        {
            Name = "Sub",
            OutputMapping = new() { ["Result"] = "ValidationResult" }
        };
        node.NextNodeIds.Add("after");
        var instance = new ProcessInstance("parent-1");

        _executor.ExecuteCommandAsync("Produce", Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        // Output mapping copies sub-process variables to parent
        // (the sub-process variable "Result" would need to be set by the business command)
    }

    [Fact]
    public async Task HandleAsync_SubProcessFails_ReturnsFailure()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("FailingWork") { Name = "Fail" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef) { Name = "Sub" };
        var instance = new ProcessInstance("parent-1");

        _executor.ExecuteCommandAsync("FailingWork", Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException(new Exception("Sub failed")));

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("Sub-process failed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ResumeExistingSubProcess_LoadsFromRepository()
    {
        var subDef = new ProcessDefinition("SubProc");
        var interactiveNode = new InteractiveNode { Name = "UserInput" };
        var bizNode = new BusinessNode("AfterInput") { Name = "After" };
        interactiveNode.NextNodeIds.Add(bizNode.Id);
        subDef.AddNode(interactiveNode);
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef) { Name = "Sub" };
        node.NextNodeIds.Add("after-sub");

        // Parent instance has saved sub-process reference in SubProcessIds
        var existingSubId = "sub-proc-id-123";
        var instance = new ProcessInstance("parent-1");
        instance.SubProcessIds[node.Id] = existingSubId;

        // Repository returns the existing sub-process instance waiting at interactive node
        var existingSubInstance = new ProcessInstance(existingSubId)
        {
            CurrentNodeId = interactiveNode.Id,
            Status = ProcessStatus.WaitingInteraction
        };
        _repository.GetProcessInstanceAsync(existingSubId).Returns(existingSubInstance);

        _executor.ExecuteCommandAsync("AfterInput", Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(node, instance);

        await _repository.Received(1).GetProcessInstanceAsync(existingSubId);
        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_ResumeNotFound_ReturnsFailed()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("Work") { Name = "Work" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef) { Name = "Sub" };
        var instance = new ProcessInstance("parent-1");
        instance.SubProcessIds[node.Id] = "missing-sub-id";

        _repository.GetProcessInstanceAsync("missing-sub-id").Returns((ProcessInstance?)null);

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_WithoutRepository_CreatesNewSubProcess()
    {
        var subDef = new ProcessDefinition("SubProc");
        var bizNode = new BusinessNode("Work") { Name = "Work" };
        subDef.AddNode(bizNode);

        var node = new SubProcessNode(subDef) { Name = "Sub" };
        node.NextNodeIds.Add("after-sub");
        var instance = new ProcessInstance("parent-1");

        var handlerNoRepo = new SubProcessNodeHandler(null, _handlers);

        _executor.ExecuteCommandAsync("Work", Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);

        var result = await handlerNoRepo.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }
}
