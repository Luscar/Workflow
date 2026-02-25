using SimpleBPM;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Abstractions;
using NSubstitute;

namespace SimpleBPM.Tests;

public class BusinessNodeHandlerTests
{
    [Fact]
    public async Task HandleAsync_Success_ReturnsCompleted()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new BusinessNodeHandler(executor);

        var node = new BusinessNode("CreateOrder") { Name = "step1", DisplayName = "Step 1" };
        node.NextNodeIds.Add("step2");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("step2", result.NextNodeName);
        await executor.Received(1).ExecuteCommandAsync("CreateOrder", 1, null, null);
    }

    [Fact]
    public async Task HandleAsync_WithParameters_PassesParameters()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new BusinessNodeHandler(executor);

        var node = new BusinessNode("ProcessPayment") { Name = "pay", DisplayName = "Pay" };
        node.Parameters["amount"] = 100.0;
        var instance = new ProcessInstance(1, 1L);

        await handler.HandleAsync(node, instance);

        await executor.Received(1).ExecuteCommandAsync(
            "ProcessPayment", 1, "AGG-1",
            Arg.Is<Dictionary<string, object>>(d => d.ContainsKey("amount")));
    }

    [Fact]
    public async Task HandleAsync_ExecutorThrows_ReturnsFailed()
    {
        var executor = Substitute.For<IBpmMediateur>();
        executor.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())
            .ThrowsAsync(new Exception("Database error"));

        var handler = new BusinessNodeHandler(executor);
        var node = new BusinessNode("FailCmd") { Name = "fail", DisplayName = "Fail" };
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Database error", result.ErrorMessage);
    }

    [Fact]
    public void Constructor_NullExecutor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessNodeHandler(null!));
    }

    [Fact]
    public void NodeType_IsBusiness()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new BusinessNodeHandler(executor);

        Assert.Equal(NodeType.Business, handler.NodeType);
    }
}

public class DecisionNodeHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithConditions_MatchesFirst()
    {
        var handler = new DecisionNodeHandler();
        var node = new DecisionNode() { Name = "decide", DisplayName = "Decide" };
        node.AddCondition("statut", "approuve", OperateurFiltre.Egal, TypeDonnee.Texte, "approved");
        node.AddCondition("statut", "refuse", OperateurFiltre.Egal, TypeDonnee.Texte, "rejected");

        var instance = new ProcessInstance(1);
        instance.Variables["statut"] = "approuve";

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.Equal("approved", result.NextNodeName);
    }

    [Fact]
    public async Task HandleAsync_WithConditions_DefaultNode()
    {
        var handler = new DecisionNodeHandler();
        var node = new DecisionNode() { Name = "decide", DisplayName = "Decide" };
        node.AddCondition("statut", "approuve", OperateurFiltre.Egal, TypeDonnee.Texte, "approved");
        node.SetNoeudParDefaut("fallback");

        var instance = new ProcessInstance(1);
        instance.Variables["statut"] = "autre";

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.Equal("fallback", result.NextNodeName);
    }

    [Fact]
    public async Task HandleAsync_WithConditions_NoMatch_NoDefault_Fails()
    {
        var handler = new DecisionNodeHandler();
        var node = new DecisionNode() { Name = "decide", DisplayName = "Decide" };
        node.AddCondition("statut", "approuve", OperateurFiltre.Egal, TypeDonnee.Texte, "approved");

        var instance = new ProcessInstance(1);
        instance.Variables["statut"] = "autre";

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_WithQuery_RoutesCorrectly()
    {
        var executor = Substitute.For<IBpmMediateur>();
        executor.EvaluateDecisionAsync("CheckEligibility", 1, null, null)
            .Returns("eligible");

        var handler = new DecisionNodeHandler(executor);
        var node = new DecisionNode("CheckEligibility") { Name = "check", DisplayName = "Check" };
        node.AddRoute("eligible", "processApproval");
        node.AddRoute("ineligible", "processRejection");

        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.Equal("processApproval", result.NextNodeName);
    }

    [Fact]
    public async Task HandleAsync_WithQuery_NoMatchingRoute_Fails()
    {
        var executor = Substitute.For<IBpmMediateur>();
        executor.EvaluateDecisionAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())
            .Returns("unknown-result");

        var handler = new DecisionNodeHandler(executor);
        var node = new DecisionNode("CheckSomething") { Name = "check", DisplayName = "Check" };
        node.AddRoute("yes", "nodeA");
        node.AddRoute("no", "nodeB");

        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("unknown-result", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_WithQuery_NoExecutor_Fails()
    {
        var handler = new DecisionNodeHandler();
        var node = new DecisionNode("SomeQuery") { Name = "query", DisplayName = "Query" };
        // No conditions, so it goes to query evaluation path

        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
    }

    [Fact]
    public void NodeType_IsDecision()
    {
        var handler = new DecisionNodeHandler();
        Assert.Equal(NodeType.Decision, handler.NodeType);
    }
}

public class InteractiveNodeHandlerTests
{
    [Fact]
    public async Task HandleAsync_SetsWaitingInteractionStatus()
    {
        var handler = new InteractiveNodeHandler();
        var node = new InteractiveNode { Name = "review", DisplayName = "Review" };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal("next", result.NextNodeName);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);
        Assert.Equal("review", instance.CurrentNodeName);
    }

    [Fact]
    public async Task HandleAsync_WithGestionTache_CreatesTask()
    {
        var gestionTache = Substitute.For<IGestionTache>();
        var handler = new InteractiveNodeHandler(gestionTache);
        var node = new InteractiveNode { Name = "review", DisplayName = "Review" };
        var instance = new ProcessInstance(1, 1L) { DefinitionName = "Process1" };

        await handler.HandleAsync(node, instance);

        await gestionTache.Received(1).CreerTacheAsync(1, 1L, "Process1", "Review");
    }

    [Fact]
    public async Task OnLeaveAsync_WithGestionTache_ClosesTask()
    {
        var gestionTache = Substitute.For<IGestionTache>();
        var handler = new InteractiveNodeHandler(gestionTache);
        var node = new InteractiveNode { Name = "review", DisplayName = "Review" };
        var instance = new ProcessInstance(1, 1L) { DefinitionName = "Process1" };

        await handler.OnLeaveAsync(node, instance);

        await gestionTache.Received(1).FermerTacheAsync(1, 1L, "Process1", "Review");
    }

    [Fact]
    public void NodeType_IsInteractive()
    {
        var handler = new InteractiveNodeHandler();
        Assert.Equal(NodeType.Interactive, handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_WithOnEnterCommandName_ExecutesCommand()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new InteractiveNodeHandler(executor: executor);
        var node = new InteractiveNode { Name = "review", DisplayName = "Review", OnEnterCommandName = "NotifyReviewPending" };
        var instance = new ProcessInstance(1, 1L);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        await executor.Received(1).ExecuteCommandAsync("NotifyReviewPending", 1, 1L, null);
    }

    [Fact]
    public async Task HandleAsync_WithoutOnEnterCommandName_DoesNotCallExecutor()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new InteractiveNodeHandler(executor: executor);
        var node = new InteractiveNode { Name = "review", DisplayName = "Review" };
        var instance = new ProcessInstance(1);

        await handler.HandleAsync(node, instance);

        await executor.DidNotReceive().ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
    }

    [Fact]
    public async Task HandleAsync_OnEnterCommandThrows_ReturnsFailed()
    {
        var executor = Substitute.For<IBpmMediateur>();
        executor.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())
            .ThrowsAsync(new Exception("Command failed"));
        var handler = new InteractiveNodeHandler(executor: executor);
        var node = new InteractiveNode { Name = "review", DisplayName = "Review", OnEnterCommandName = "NotifyReviewPending" };
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Command failed", result.ErrorMessage);
    }
}

public class WaitForSignalNodeHandlerTests
{
    [Fact]
    public async Task HandleAsync_SetsWaitingSignalStatus()
    {
        var handler = new WaitForSignalNodeHandler();
        var node = new WaitForSignalNode("approval-signal")
        {
            Name = "waitApproval",
            DisplayName = "Wait Approval"
        };
        node.NextNodeIds.Add("nextStep");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal("nextStep", result.NextNodeName);
        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);
        Assert.Equal("approval-signal", instance.InternalState["WaitingForSignal"]?.ToString());
    }

    [Fact]
    public void NodeType_IsWaitForSignal()
    {
        var handler = new WaitForSignalNodeHandler();
        Assert.Equal(NodeType.WaitForSignal, handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_WithOnEnterCommandName_ExecutesCommand()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new WaitForSignalNodeHandler(executor);
        var node = new WaitForSignalNode("approval-signal")
        {
            Name = "waitApproval",
            DisplayName = "Wait Approval",
            OnEnterCommandName = "NotifyAwaitingApproval"
        };
        var instance = new ProcessInstance(1, 1L);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        await executor.Received(1).ExecuteCommandAsync("NotifyAwaitingApproval", 1, 1L, null);
    }

    [Fact]
    public async Task HandleAsync_WithoutOnEnterCommandName_DoesNotCallExecutor()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new WaitForSignalNodeHandler(executor);
        var node = new WaitForSignalNode("approval-signal") { Name = "waitApproval", DisplayName = "Wait Approval" };
        var instance = new ProcessInstance(1);

        await handler.HandleAsync(node, instance);

        await executor.DidNotReceive().ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
    }

    [Fact]
    public async Task HandleAsync_OnEnterCommandThrows_ReturnsFailed()
    {
        var executor = Substitute.For<IBpmMediateur>();
        executor.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())
            .ThrowsAsync(new Exception("Command failed"));
        var handler = new WaitForSignalNodeHandler(executor);
        var node = new WaitForSignalNode("signal") { Name = "wait", DisplayName = "Wait", OnEnterCommandName = "Notify" };
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Command failed", result.ErrorMessage);
    }
}

public class WaitUntilDateNodeHandlerTests
{
    [Fact]
    public async Task HandleAsync_PastDate_Continues()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddDays(-1))
        {
            Name = "wait",
            DisplayName = "Wait"
        };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("next", result.NextNodeName);
    }

    [Fact]
    public async Task HandleAsync_FutureDate_Stops()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddDays(5))
        {
            Name = "wait",
            DisplayName = "Wait"
        };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);
    }

    [Fact]
    public async Task HandleAsync_NoDate_Fails()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode() { Name = "wait", DisplayName = "Wait" };
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No target date", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_WithDateKey_ReadsFromVariables()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode("DueDate") { Name = "wait", DisplayName = "Wait" };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1);
        instance.Variables["DueDate"] = DateTime.UtcNow.AddDays(-1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_WithDateProvider_UsesProvider()
    {
        var handler = new WaitUntilDateNodeHandler();
        var node = new WaitUntilDateNode(instance => DateTime.UtcNow.AddDays(-1))
        {
            Name = "wait",
            DisplayName = "Wait"
        };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
    }

    [Fact]
    public void NodeType_IsWaitUntilDate()
    {
        var handler = new WaitUntilDateNodeHandler();
        Assert.Equal(NodeType.WaitUntilDate, handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_FutureDate_WithOnEnterCommandName_ExecutesCommand()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new WaitUntilDateNodeHandler(executor);
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddDays(5))
        {
            Name = "wait",
            DisplayName = "Wait",
            OnEnterCommandName = "NotifyWaiting"
        };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1, 1L);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        await executor.Received(1).ExecuteCommandAsync("NotifyWaiting", 1, 1L, null);
    }

    [Fact]
    public async Task HandleAsync_PastDate_WithOnEnterCommandName_DoesNotExecuteCommand()
    {
        var executor = Substitute.For<IBpmMediateur>();
        var handler = new WaitUntilDateNodeHandler(executor);
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddDays(-1))
        {
            Name = "wait",
            DisplayName = "Wait",
            OnEnterCommandName = "NotifyWaiting"
        };
        node.NextNodeIds.Add("next");
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        await executor.DidNotReceive().ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
    }

    [Fact]
    public async Task HandleAsync_FutureDate_OnEnterCommandThrows_ReturnsFailed()
    {
        var executor = Substitute.For<IBpmMediateur>();
        executor.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())
            .ThrowsAsync(new Exception("Command failed"));
        var handler = new WaitUntilDateNodeHandler(executor);
        var node = new WaitUntilDateNode(DateTime.UtcNow.AddDays(5)) { Name = "wait", DisplayName = "Wait", OnEnterCommandName = "Notify" };
        var instance = new ProcessInstance(1);

        var result = await handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Equal("Command failed", result.ErrorMessage);
    }
}
