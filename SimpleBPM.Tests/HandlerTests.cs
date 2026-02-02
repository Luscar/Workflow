using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Tests.Helpers;

namespace SimpleBPM.Tests;

public class HandlerTests
{
    // ========== BusinessNodeHandler ==========

    [Fact]
    public async Task BusinessHandler_ExecutesCommand_ReturnsSuccess()
    {
        var executor = new FakeCommandExecutor();
        var handler = new BusinessNodeHandler(executor);
        var node = new BusinessNode("DoWork") { Name = "Work" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("next-id", result.NextNodeId);
        Assert.Equal(new[] { "DoWork" }, executor.ExecutedCommands);
    }

    [Fact]
    public async Task BusinessHandler_ExecutorThrows_ReturnsFailed()
    {
        var executor = new FakeCommandExecutor()
            .WithOnExecute(_ => throw new Exception("Boom"));
        var handler = new BusinessNodeHandler(executor);
        var node = new BusinessNode("Fail") { Name = "Fail" };
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Boom", result.ErrorMessage);
    }

    [Fact]
    public void BusinessHandler_NullExecutor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessNodeHandler(null!));
    }

    // ========== DecisionNodeHandler ==========

    [Fact]
    public async Task DecisionHandler_MatchingRoute_ReturnsCorrectNext()
    {
        var executor = new FakeCommandExecutor()
            .WithDecisionResult("Check", "approved");
        var handler = new DecisionNodeHandler(executor);

        var node = new DecisionNode("Check") { Name = "Decision" };
        node.AddRoute("approved", "approve-id");
        node.AddRoute("rejected", "reject-id");
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.Equal("approve-id", result.NextNodeId);
    }

    [Fact]
    public async Task DecisionHandler_NoMatchingRoute_ReturnsFailed()
    {
        var executor = new FakeCommandExecutor()
            .WithDecisionResult("Check", "unknown");
        var handler = new DecisionNodeHandler(executor);

        var node = new DecisionNode("Check") { Name = "Decision" };
        node.AddRoute("yes", "yes-id");
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No route found", result.ErrorMessage);
    }

    [Fact]
    public async Task DecisionHandler_ExecutorThrows_ReturnsFailed()
    {
        var executor = new FakeCommandExecutor()
            .WithOnExecute(_ => throw new Exception("Decision error"));
        // Override the decision evaluation to also throw
        var throwingExecutor = new ThrowingDecisionExecutor();
        var handler = new DecisionNodeHandler(throwingExecutor);

        var node = new DecisionNode("Check") { Name = "Decision" };
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Decision failed", result.ErrorMessage);
    }

    // ========== InteractiveNodeHandler ==========

    [Fact]
    public async Task InteractiveHandler_PausesProcess()
    {
        var handler = new InteractiveNodeHandler();
        var node = new InteractiveNode { Name = "Review" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);
        Assert.Equal(node.Id, instance.CurrentNodeId);
    }

    [Fact]
    public async Task InteractiveHandler_WithGestionTache_CreatesTask()
    {
        var gestionTache = new FakeGestionTache();
        var handler = new InteractiveNodeHandler(gestionTache);
        var node = new InteractiveNode { Name = "Approval" };
        var instance = new ProcessInstance("p1") { DefinitionName = "Process" };

        await handler.HandleAsync(node, instance);

        Assert.Single(gestionTache.CreatedTasks, "Approval");
    }

    [Fact]
    public async Task InteractiveHandler_OnLeave_ClosesTask()
    {
        var gestionTache = new FakeGestionTache();
        var handler = new InteractiveNodeHandler(gestionTache);
        var node = new InteractiveNode { Name = "Approval" };
        var instance = new ProcessInstance("p1") { DefinitionName = "Process" };

        await handler.OnLeaveAsync(node, instance);

        Assert.Single(gestionTache.ClosedTasks, "Approval");
    }

    [Fact]
    public async Task InteractiveHandler_WithoutGestionTache_NoError()
    {
        var handler = new InteractiveNodeHandler();
        var node = new InteractiveNode { Name = "Review" };
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
    }

    // ========== WaitForSignalNodeHandler ==========

    [Fact]
    public async Task WaitForSignalHandler_PausesAndStoresSignalName()
    {
        var handler = new WaitForSignalNodeHandler();
        var node = new WaitForSignalNode("PaymentReceived") { Name = "WaitPayment" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);
        Assert.Equal("PaymentReceived", instance.InternalState["WaitingForSignal"]);
    }

    // ========== WaitUntilDateNodeHandler ==========

    [Fact]
    public async Task WaitUntilDateHandler_PastDate_ContinuesImmediately()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddHours(-1)) { Name = "WaitDate" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("next-id", result.NextNodeId);
    }

    [Fact]
    public async Task WaitUntilDateHandler_FutureDate_PausesProcess()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddHours(1)) { Name = "WaitDate" };
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);
    }

    [Fact]
    public async Task WaitUntilDateHandler_DateFromVariable_Works()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode("dueDate") { Name = "WaitDate" };
        var instance = new ProcessInstance("p1");
        instance.Variables["dueDate"] = DateTime.UtcNow.AddHours(-1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }

    [Fact]
    public async Task WaitUntilDateHandler_StringDateVariable_Parsed()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode("dueDate") { Name = "WaitDate" };
        var instance = new ProcessInstance("p1");
        instance.Variables["dueDate"] = "2020-01-01T00:00:00Z";

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }

    [Fact]
    public async Task WaitUntilDateHandler_NoDateConfigured_Fails()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode { Name = "WaitDate" };
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No target date", result.ErrorMessage);
    }

    [Fact]
    public async Task WaitUntilDateHandler_DateProvider_Works()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode(_ => DateTime.UtcNow.AddHours(-1)) { Name = "WaitDate" };
        var instance = new ProcessInstance("p1");

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }
}

// Helper for testing decision handler exception handling
file class ThrowingDecisionExecutor : SimpleBPM.Abstractions.ICommandExecutor
{
    public Task ExecuteCommandAsync(string commandName, string processId, string? aggregateId) =>
        Task.CompletedTask;

    public Task<string> EvaluateDecisionAsync(string decisionName, string processId, string? aggregateId) =>
        throw new Exception("Decision failed");
}
