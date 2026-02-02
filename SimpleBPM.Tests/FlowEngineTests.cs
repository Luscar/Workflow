using SimpleBPM.Definition;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Tests.Helpers;

namespace SimpleBPM.Tests;

public class FlowEngineTests
{
    private readonly FakeCommandExecutor _executor = new();

    private INodeHandler[] CreateHandlers() => new INodeHandler[]
    {
        new BusinessNodeHandler(_executor),
        new DecisionNodeHandler(_executor)
    };

    [Fact]
    public async Task ExecuteAsync_LinearProcess_CompletesAllNodes()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Business("Step2")
            .Business("Step3")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal(3, result.ExecutionHistory.Count);
        Assert.Equal(new[] { "Step1", "Step2", "Step3" }, _executor.ExecutedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_DecisionNode_RoutesToCorrectBranch()
    {
        _executor.WithDecisionResult("CheckAmount", "approved");

        var definition = ProcessBuilder.Create("Test")
            .Business("Validate")
            .Decision("CheckAmount", "Check", routes => routes
                .When("approved", "Approve")
                .When("rejected", "Reject"))
            .Business("Approve", "Approved")
            .Business("Reject", "Rejected")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Contains("Validate", _executor.ExecutedCommands);
        Assert.Contains("Approve", _executor.ExecutedCommands);
        Assert.DoesNotContain("Reject", _executor.ExecutedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_DecisionNode_NoMatchingRoute_Fails()
    {
        _executor.WithDecisionResult("Check", "unknown");

        var definition = ProcessBuilder.Create("Test")
            .Business("Start")
            .Decision("Check", routes => routes
                .When("yes", "End"))
            .Business("End")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Failed, result.Status);
        Assert.Contains("No route found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_InteractiveNode_PausesProcess()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Interactive("Wait", "Waiting")
            .Business("Step2")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingInteraction, result.Status);
        Assert.Equal(new[] { "Step1" }, _executor.ExecutedCommands);
    }

    [Fact]
    public async Task ContinueAsync_AfterInteractive_ResumesAndCompletes()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Interactive("Wait", "Waiting")
            .Business("Step2")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        instance = await engine.ContinueAsync(instance);
        Assert.Equal(ProcessStatus.Completed, instance.Status);
        Assert.Equal(new[] { "Step1", "Step2" }, _executor.ExecutedCommands);
    }

    [Fact]
    public async Task SignalAsync_CorrectSignal_ContinuesProcess()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .WaitForSignal("PaymentReceived", "Wait")
            .Business("Step2")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);

        instance = await engine.SignalAsync(instance, "PaymentReceived");
        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task SignalAsync_WrongSignal_DoesNotAdvance()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .WaitForSignal("PaymentReceived", "Wait")
            .Business("Step2")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        instance = await engine.ExecuteAsync(instance);
        instance = await engine.SignalAsync(instance, "WrongSignal");

        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);
    }

    [Fact]
    public async Task SignalAsync_NotWaiting_Throws()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");
        instance = await engine.ExecuteAsync(instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SignalAsync(instance, "Signal"));
    }

    [Fact]
    public async Task ContinueAsync_CompletedProcess_Throws()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");
        instance = await engine.ExecuteAsync(instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ContinueAsync(instance));
    }

    [Fact]
    public async Task ExecuteAsync_RecordsExecutionHistory()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1", "First Step")
            .Business("Step2", "Second Step")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(2, result.ExecutionHistory.Count);
        Assert.All(result.ExecutionHistory, h => Assert.True(h.Success));
        Assert.Equal("First Step", result.ExecutionHistory[0].NodeName);
        Assert.Equal("Second Step", result.ExecutionHistory[1].NodeName);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepository_PersistsInstance()
    {
        var repository = new InMemoryProcessRepository();
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, repository, CreateHandlers());
        var instance = new ProcessInstance("p1");

        await engine.ExecuteAsync(instance);

        var loaded = await repository.GetProcessInstanceAsync("p1");
        Assert.NotNull(loaded);
        Assert.Equal(ProcessStatus.Completed, loaded.Status);
    }

    [Fact]
    public async Task ExecuteAsync_SetsDefinitionNameAndVersion()
    {
        var definition = ProcessBuilder.Create("OrderProcess", "2.0")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal("OrderProcess", result.DefinitionName);
        Assert.Equal("2.0", result.DefinitionVersion);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleDefinitions_ResolvesCorrectOne()
    {
        var def1 = ProcessBuilder.Create("ProcessA")
            .Business("A1")
            .Build();
        var def2 = ProcessBuilder.Create("ProcessB")
            .Business("B1")
            .Build();

        var engine = new FlowEngine(new[] { def1, def2 }, handlers: CreateHandlers());

        var instance = new ProcessInstance("p1") { DefinitionName = "ProcessB" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal("ProcessB", result.DefinitionName);
        Assert.Equal(new[] { "B1" }, _executor.ExecutedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_CommandThrows_FailsProcess()
    {
        _executor.WithOnExecute(cmd =>
        {
            if (cmd == "FailingCommand") throw new Exception("Command failed");
            return Task.CompletedTask;
        });

        var definition = ProcessBuilder.Create("Test")
            .Business("FailingCommand")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Failed, result.Status);
        Assert.Equal("Command failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_VariablesArePreserved()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");
        instance.Variables["Key"] = "Value";

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal("Value", result.Variables["Key"]);
    }

    [Fact]
    public async Task LoadProcessAsync_WithRepository_ReturnsInstance()
    {
        var repository = new InMemoryProcessRepository();
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, repository, CreateHandlers());
        var instance = new ProcessInstance("p1");
        await engine.ExecuteAsync(instance);

        var loaded = await engine.LoadProcessAsync("p1");

        Assert.NotNull(loaded);
        Assert.Equal("p1", loaded.ProcessId);
    }

    [Fact]
    public async Task LoadProcessAsync_WithoutRepository_Throws()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Build();

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.LoadProcessAsync("p1"));
    }

    [Fact]
    public async Task ExecuteAsync_WaitUntilDate_PastDate_Continues()
    {
        var definition = new ProcessDefinition("Test");
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddHours(-1)) { Name = "WaitNode" };
        var endNode = new BusinessNode("End") { Name = "End" };
        node.NextNodeIds.Add(endNode.Id);
        definition.AddNode(node).AddNode(endNode);

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WaitUntilDate_FutureDate_Pauses()
    {
        var definition = new ProcessDefinition("Test");
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddHours(1)) { Name = "WaitNode" };
        definition.AddNode(node);

        var engine = new FlowEngine(new[] { definition }, handlers: CreateHandlers());
        var instance = new ProcessInstance("p1");

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingDate, result.Status);
    }
}
