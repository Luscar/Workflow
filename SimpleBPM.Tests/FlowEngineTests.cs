using SimpleBPM;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Abstractions;
using NSubstitute;

namespace SimpleBPM.Tests;

public class FlowEngineTests
{
    private static ProcessDefinition CreateSimpleBusinessProcess()
    {
        var def = new ProcessDefinition("SimpleProcess", "1.0");
        var node1 = new BusinessNode("Step1") { Name = "Step1", DisplayName = "Étape 1" };
        var node2 = new BusinessNode("Step2") { Name = "Step2", DisplayName = "Étape 2" };
        node1.NextNodeIds.Add("Step2");
        def.AddNode(node1);
        def.AddNode(node2);
        def.StartNodeId = "Step1";
        return def;
    }

    private static INodeHandler CreateSuccessfulBusinessHandler()
    {
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<NodeDefinition>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeName = node.NextNodeIds.FirstOrDefault()
                });
            });
        return handler;
    }

    [Fact]
    public async Task ExecuteAsync_SimpleProcess_CompletesSuccessfully()
    {
        var def = CreateSimpleBusinessProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "SimpleProcess" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Null(result.CurrentNodeName);
        Assert.NotNull(result.CompletedAt);
        Assert.Equal(2, result.ExecutionHistory.Count);
    }

    [Fact]
    public async Task ExecuteAsync_SetsDefinitionInfo()
    {
        var def = CreateSimpleBusinessProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "SimpleProcess" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal("SimpleProcess", result.DefinitionName);
        Assert.Equal("1.0", result.DefinitionVersion);
    }

    [Fact]
    public async Task ExecuteAsync_NodeNotFound_Fails()
    {
        var def = new ProcessDefinition("Test", "1.0");
        var node = new BusinessNode("Step1") { Name = "Step1", DisplayName = "Step 1" };
        node.NextNodeIds.Add("NonExistent");
        def.AddNode(node);

        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Failed, result.Status);
        Assert.Contains("NonExistent", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_NoHandler_Fails()
    {
        var def = new ProcessDefinition("Test", "1.0");
        def.AddNode(new BusinessNode("Step1") { Name = "Step1", DisplayName = "Step 1" });

        // No handlers registered
        var engine = new FlowEngine(new[] { def }, handlers: Array.Empty<INodeHandler>());

        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Failed, result.Status);
        Assert.Contains("No handler", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerFails_ProcessFails()
    {
        var def = new ProcessDefinition("Test", "1.0");
        def.AddNode(new BusinessNode("Step1") { Name = "Step1", DisplayName = "Step 1" });

        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(Task.FromResult(new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = "Command execution failed"
            }));

        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Failed, result.Status);
        Assert.Equal("Command execution failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_InteractiveNode_StopsExecution()
    {
        var def = new ProcessDefinition("Test", "1.0");
        var interactive = new InteractiveNode { Name = "Review", DisplayName = "Review Step" };
        var business = new BusinessNode("Final") { Name = "Final", DisplayName = "Final Step" };
        interactive.NextNodeIds.Add("Final");
        def.AddNode(interactive);
        def.AddNode(business);

        var engine = new FlowEngine(new[] { def });

        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingInteraction, result.Status);
        Assert.Equal("Review", result.CurrentNodeName);
    }

    [Fact]
    public async Task ContinueAsync_ResumesFromCurrentNode()
    {
        var def = new ProcessDefinition("Test", "1.0");
        var interactive = new InteractiveNode { Name = "Review", DisplayName = "Review" };
        var final = new BusinessNode("Final") { Name = "Final", DisplayName = "Final" };
        interactive.NextNodeIds.Add("Final");
        def.AddNode(interactive);
        def.AddNode(final);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });

        // First: execute until interactive stop
        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // Then: continue
        instance = await engine.ContinueAsync(instance);

        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task ContinueAsync_CompletedProcess_Throws()
    {
        var def = CreateSimpleBusinessProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "SimpleProcess" };
        instance = await engine.ExecuteAsync(instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueAsync(instance));
    }

    [Fact]
    public async Task ContinueAsync_FailedProcess_Throws()
    {
        var def = new ProcessDefinition("Test", "1.0");
        var engine = new FlowEngine(new[] { def });

        var instance = new ProcessInstance(1)
        {
            DefinitionName = "Test",
            Status = ProcessStatus.Failed
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueAsync(instance));
    }

    [Fact]
    public async Task SignalAsync_WaitingSignal_ContinuesProcess()
    {
        var def = new ProcessDefinition("Test", "1.0");
        var signalNode = new WaitForSignalNode("approval") { Name = "WaitApproval", DisplayName = "Wait Approval" };
        var final = new BusinessNode("Final") { Name = "Final", DisplayName = "Final" };
        signalNode.NextNodeIds.Add("Final");
        def.AddNode(signalNode);
        def.AddNode(final);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        instance = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);
        Assert.Equal("approval", instance.ExpectedSignal);

        instance = await engine.SignalAsync(instance, "approval");

        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task SignalAsync_WrongSignalName_DoesNotContinue()
    {
        var def = new ProcessDefinition("Test", "1.0");
        var signalNode = new WaitForSignalNode("approval") { Name = "WaitApproval", DisplayName = "Wait" };
        def.AddNode(signalNode);

        var engine = new FlowEngine(new[] { def });

        var instance = new ProcessInstance(1) { DefinitionName = "Test" };
        instance = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);

        instance = await engine.SignalAsync(instance, "wrong-signal");

        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);
    }

    [Fact]
    public async Task SignalAsync_NotWaiting_Throws()
    {
        var def = CreateSimpleBusinessProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "SimpleProcess" };
        instance = await engine.ExecuteAsync(instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SignalAsync(instance, "signal"));
    }

    [Fact]
    public async Task ExecuteAsync_DecisionNode_WithConditions_RoutesCorrectly()
    {
        var def = new ProcessDefinition("DecisionTest", "1.0");
        var decision = new DecisionNode()
        {
            Name = "CheckAmount",
            DisplayName = "Check Amount"
        };
        decision.AddCondition("montant", 1000.0, OperateurFiltre.Superieur, TypeDonnee.Nombre, "HighValue");
        decision.SetNoeudParDefaut("LowValue");

        var highNode = new BusinessNode("ProcessHigh") { Name = "HighValue", DisplayName = "High" };
        var lowNode = new BusinessNode("ProcessLow") { Name = "LowValue", DisplayName = "Low" };

        def.AddNode(decision);
        def.AddNode(highNode);
        def.AddNode(lowNode);
        def.StartNodeId = "CheckAmount";

        var businessHandler = CreateSuccessfulBusinessHandler();
        var decisionHandler = new DecisionNodeHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new INodeHandler[] { businessHandler, decisionHandler });

        // High value case
        var instance = new ProcessInstance(1) { DefinitionName = "DecisionTest" };
        instance.Variables["montant"] = 1500.0;
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.True(result.ExecutionHistory.Any(h => h.NodeId == "HighValue"));
    }

    [Fact]
    public async Task ExecuteAsync_DecisionNode_DefaultRoute()
    {
        var def = new ProcessDefinition("DecisionTest", "1.0");
        var decision = new DecisionNode()
        {
            Name = "CheckAmount",
            DisplayName = "Check Amount"
        };
        decision.AddCondition("montant", 1000.0, OperateurFiltre.Superieur, TypeDonnee.Nombre, "HighValue");
        decision.SetNoeudParDefaut("LowValue");

        var highNode = new BusinessNode("ProcessHigh") { Name = "HighValue", DisplayName = "High" };
        var lowNode = new BusinessNode("ProcessLow") { Name = "LowValue", DisplayName = "Low" };

        def.AddNode(decision);
        def.AddNode(highNode);
        def.AddNode(lowNode);
        def.StartNodeId = "CheckAmount";

        var businessHandler = CreateSuccessfulBusinessHandler();
        var decisionHandler = new DecisionNodeHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new INodeHandler[] { businessHandler, decisionHandler });

        // Low value case (default route)
        var instance = new ProcessInstance(2) { DefinitionName = "DecisionTest" };
        instance.Variables["montant"] = 500.0;
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.True(result.ExecutionHistory.Any(h => h.NodeId == "LowValue"));
    }

    [Fact]
    public async Task ExecuteAsync_WaitUntilDate_PastDate_Continues()
    {
        var def = new ProcessDefinition("WaitTest", "1.0");
        var waitNode = new WaitUntilDateNode(DateTime.UtcNow.AddDays(-1))
        {
            Name = "Wait",
            DisplayName = "Wait"
        };
        var final = new BusinessNode("Final") { Name = "Final", DisplayName = "Final" };
        waitNode.NextNodeIds.Add("Final");
        def.AddNode(waitNode);
        def.AddNode(final);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "WaitTest" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WaitUntilDate_FutureDate_Stops()
    {
        var def = new ProcessDefinition("WaitTest", "1.0");
        var waitNode = new WaitUntilDateNode(DateTime.UtcNow.AddDays(5))
        {
            Name = "Wait",
            DisplayName = "Wait"
        };
        def.AddNode(waitNode);

        var engine = new FlowEngine(new[] { def });

        var instance = new ProcessInstance(1) { DefinitionName = "WaitTest" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingDate, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WaitUntilDate_WithDateKey_StoresDateInWaitDate()
    {
        var def = new ProcessDefinition("WaitTest", "1.0");
        var waitNode = new WaitUntilDateNode("targetDate")
        {
            Name = "Wait",
            DisplayName = "Wait"
        };
        def.AddNode(waitNode);

        var engine = new FlowEngine(new[] { def });

        var futureDate = DateTime.UtcNow.AddDays(5);
        var instance = new ProcessInstance(1) { DefinitionName = "WaitTest" };
        instance.Variables["targetDate"] = futureDate;

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.WaitingDate, result.Status);
        Assert.Equal(futureDate, result.WaitDate);
    }

    [Fact]
    public async Task ExecuteAsync_WaitUntilDate_WithDateKey_UsesStoredDateOnResume()
    {
        var def = new ProcessDefinition("WaitTest", "1.0");
        var waitNode = new WaitUntilDateNode("targetDate")
        {
            Name = "Wait",
            DisplayName = "Wait"
        };
        var final = new BusinessNode("Final") { Name = "Final", DisplayName = "Final" };
        waitNode.NextNodeIds.Add("Final");
        def.AddNode(waitNode);
        def.AddNode(final);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });

        // Première exécution avec une date future
        var futureDate = DateTime.UtcNow.AddDays(5);
        var instance = new ProcessInstance(1) { DefinitionName = "WaitTest" };
        instance.Variables["targetDate"] = futureDate;
        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);

        // Simuler une reprise : la date est passée, la variable a changé
        instance.WaitDate = DateTime.UtcNow.AddDays(-1);
        instance.Variables["targetDate"] = DateTime.UtcNow.AddDays(10); // variable modifiée

        instance = await engine.ContinueAsync(instance);

        // Le processus doit continuer grâce à la date de la banque, pas de la variable
        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    [Fact]
    public void GetLatestDefinition_ReturnsHighestVersion()
    {
        var def1 = new ProcessDefinition("MyProcess", "1.0");
        def1.AddNode(new NodeDefinition(NodeType.Business) { Name = "n1" });
        var def2 = new ProcessDefinition("MyProcess", "2.0");
        def2.AddNode(new NodeDefinition(NodeType.Business) { Name = "n1" });

        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def1, def2 }, handlers: new[] { handler });

        var latest = engine.GetLatestDefinition("MyProcess");

        Assert.Equal("2.0", latest.Version);
    }

    [Fact]
    public void GetDefinition_ReturnsSpecificVersion()
    {
        var def1 = new ProcessDefinition("MyProcess", "1.0");
        def1.AddNode(new NodeDefinition(NodeType.Business) { Name = "n1" });
        var def2 = new ProcessDefinition("MyProcess", "2.0");
        def2.AddNode(new NodeDefinition(NodeType.Business) { Name = "n1" });

        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def1, def2 }, handlers: new[] { handler });

        var specific = engine.GetDefinition("MyProcess", "1.0");

        Assert.Equal("1.0", specific.Version);
    }

    [Fact]
    public void GetDefinition_NotFound_Throws()
    {
        var def = new ProcessDefinition("Test", "1.0");
        def.AddNode(new NodeDefinition(NodeType.Business) { Name = "n1" });
        var engine = new FlowEngine(new[] { def });

        Assert.Throws<InvalidOperationException>(() => engine.GetDefinition("Unknown", "1.0"));
    }

    [Fact]
    public async Task ExecuteAsync_RecordsExecutionHistory()
    {
        var def = CreateSimpleBusinessProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "SimpleProcess" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(2, result.ExecutionHistory.Count);
        Assert.Equal("Step1", result.ExecutionHistory[0].NodeId);
        Assert.Equal("Step2", result.ExecutionHistory[1].NodeId);
        Assert.All(result.ExecutionHistory, h => Assert.True(h.Success));
    }

    [Fact]
    public async Task LoadProcessAsync_WithoutRepository_Throws()
    {
        var def = CreateSimpleBusinessProcess();
        var engine = new FlowEngine(new[] { def });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.LoadProcessAsync(1));
    }
}
