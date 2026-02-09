using SimpleBPM;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;
using SimpleBPM.Abstractions;
using NSubstitute;

namespace SimpleBPM.Tests;

/// <summary>
/// High-level integration tests verifying content (variables/data) flows correctly
/// through the entire workflow lifecycle: creation, interactive steps, decisions,
/// signals, subprocesses, and retrieval.
/// </summary>
public class ContentIntegrationTests
{
    #region Helpers

    private static INodeHandler CreateBusinessHandler(Action<ProcessInstance>? onExecute = null)
    {
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);
                onExecute?.Invoke(inst);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });
        return handler;
    }

    private static InMemoryProcessRepository CreateInMemoryRepository()
    {
        return new InMemoryProcessRepository();
    }

    /// <summary>
    /// Simple in-memory repository for integration testing without Oracle.
    /// Tracks node history entries with auto-incrementing IDs to simulate persistence.
    /// </summary>
    internal class InMemoryProcessRepository : IProcessRepository
    {
        private long _sequence;
        private long _historySequence;
        private readonly Dictionary<long, ProcessInstance> _instances = new();
        private readonly Dictionary<long, (NodeExecutionHistory History, long ProcessId)> _historyEntries = new();

        public Task<long> ObtenirSequenceAsync(string nomSequence) =>
            Task.FromResult(Interlocked.Increment(ref _sequence));

        public Task SaveProcessInstanceAsync(ProcessInstance instance)
        {
            _instances[instance.ProcessId] = instance;
            TrackNewHistoryEntries(instance);
            return Task.CompletedTask;
        }

        public Task<ProcessInstance?> GetProcessInstanceAsync(long processId) =>
            Task.FromResult(_instances.TryGetValue(processId, out var inst) ? inst : null);

        public Task UpdateProcessInstanceAsync(ProcessInstance instance)
        {
            _instances[instance.ProcessId] = instance;
            TrackNewHistoryEntries(instance);
            return Task.CompletedTask;
        }

        public Task DeleteProcessInstanceAsync(long processId)
        {
            _instances.Remove(processId);
            return Task.CompletedTask;
        }

        public Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres)
        {
            var results = _instances.Values.Where(inst =>
                filtres.All(f =>
                    inst.Variables.TryGetValue(f.NomVariable, out var val) && f.Correspond(val)))
                .ToList();
            return Task.FromResult(results);
        }

        public Task<(NodeExecutionHistory History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId) =>
            Task.FromResult(_historyEntries.TryGetValue(historyId, out var entry)
                ? (entry.History, entry.ProcessId)
                : ((NodeExecutionHistory History, long ProcessId)?)null);

        public Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeId) =>
            Task.FromResult(_instances.Values.FirstOrDefault(i =>
                i.ParentProcessId == parentProcessId && i.ParentNodeId == parentNodeId));

        public Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId) =>
            Task.FromResult(_instances.Values.Where(i => i.ParentProcessId == parentProcessId).ToList());

        /// <summary>
        /// Returns the last history entry ID for a given process instance.
        /// </summary>
        public long? GetLastHistoryId(long processId)
        {
            return _historyEntries
                .Where(kvp => kvp.Value.ProcessId == processId)
                .Select(kvp => kvp.Key)
                .DefaultIfEmpty(-1)
                .Max();
        }

        private void TrackNewHistoryEntries(ProcessInstance instance)
        {
            var tracked = _historyEntries.Values
                .Where(e => e.ProcessId == instance.ProcessId)
                .Select(e => e.History)
                .ToHashSet();

            foreach (var entry in instance.ExecutionHistory)
            {
                if (!tracked.Contains(entry))
                {
                    var id = Interlocked.Increment(ref _historySequence);
                    _historyEntries[id] = (entry, instance.ProcessId);
                }
            }
        }
    }

    #endregion

    #region Content Flow Through Interactive Nodes

    [Fact]
    public async Task TerminerEtape_MergesContentIntoProcessVariables()
    {
        // Arrange: Process with interactive node followed by a business node
        var repository = CreateInMemoryRepository();
        var capturedVariables = new Dictionary<string, object>();

        var businessHandler = CreateBusinessHandler(inst =>
        {
            foreach (var kvp in inst.Variables)
                capturedVariables[kvp.Key] = kvp.Value;
        });

        var def = new ProcessDefinition("ContentFlow", "1.0");
        var interactive = new InteractiveNode { Name = "UserInput", DisplayName = "User Input" };
        var process = new BusinessNode("ProcessData") { Name = "ProcessData", DisplayName = "Process Data" };
        interactive.NextNodeIds.Add("ProcessData");
        def.AddNode(interactive);
        def.AddNode(process);

        var handlers = new INodeHandler[] { businessHandler };
        var service = new FlowService(new[] { def }, repository, handlers);

        // Act: Create process, it stops at interactive node
        var processId = await service.CreateProcessInstance("ContentFlow",
            new Dictionary<string, object> { ["initialKey"] = "initialValue" });

        var processus = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.WaitingInteraction, processus.Status);

        // Provide content via TerminerEtape (simulate user submitting a form)
        var lastHistoryId = repository.GetLastHistoryId(processId);
        Assert.NotNull(lastHistoryId);

        // TerminerEtape merges dictionary content into variables
        await service.TerminerEtape(lastHistoryId!.Value, new Dictionary<string, object>
        {
            ["userName"] = "Alice",
            ["amount"] = 250.0,
            ["approved"] = true
        });

        // Assert: Business node received both initial and user-submitted variables
        Assert.Equal("initialValue", capturedVariables["initialKey"]);
        Assert.Equal("Alice", capturedVariables["userName"]);
        Assert.Equal(250.0, capturedVariables["amount"]);
        Assert.Equal(true, capturedVariables["approved"]);
    }

    [Fact]
    public async Task TerminerEtape_OverwritesExistingVariables()
    {
        var repository = CreateInMemoryRepository();
        var capturedVariables = new Dictionary<string, object>();

        var businessHandler = CreateBusinessHandler(inst =>
        {
            foreach (var kvp in inst.Variables)
                capturedVariables[kvp.Key] = kvp.Value;
        });

        var def = new ProcessDefinition("OverwriteFlow", "1.0");
        var interactive = new InteractiveNode { Name = "Edit", DisplayName = "Edit" };
        var finish = new BusinessNode("Finish") { Name = "Finish", DisplayName = "Finish" };
        interactive.NextNodeIds.Add("Finish");
        def.AddNode(interactive);
        def.AddNode(finish);

        var service = new FlowService(new[] { def }, repository, new[] { businessHandler });

        var processId = await service.CreateProcessInstance("OverwriteFlow",
            new Dictionary<string, object> { ["status"] = "draft", ["priority"] = "low" });

        var lastHistoryId = repository.GetLastHistoryId(processId);
        Assert.NotNull(lastHistoryId);

        // Overwrite "status" and add new variable
        await service.TerminerEtape(lastHistoryId!.Value, new Dictionary<string, object>
        {
            ["status"] = "submitted",
            ["comment"] = "Please review"
        });

        Assert.Equal("submitted", capturedVariables["status"]);
        Assert.Equal("low", capturedVariables["priority"]);
        Assert.Equal("Please review", capturedVariables["comment"]);
    }

    [Fact]
    public async Task TerminerEtape_NonDictionaryContent_DoesNotModifyVariables()
    {
        var repository = CreateInMemoryRepository();
        var capturedVariables = new Dictionary<string, object>();

        var businessHandler = CreateBusinessHandler(inst =>
        {
            foreach (var kvp in inst.Variables)
                capturedVariables[kvp.Key] = kvp.Value;
        });

        var def = new ProcessDefinition("NonDictFlow", "1.0");
        var interactive = new InteractiveNode { Name = "Step", DisplayName = "Step" };
        var finish = new BusinessNode("Finish") { Name = "Finish", DisplayName = "Finish" };
        interactive.NextNodeIds.Add("Finish");
        def.AddNode(interactive);
        def.AddNode(finish);

        var service = new FlowService(new[] { def }, repository, new[] { businessHandler });

        var processId = await service.CreateProcessInstance("NonDictFlow",
            new Dictionary<string, object> { ["key"] = "value" });

        var lastHistoryId = repository.GetLastHistoryId(processId);
        Assert.NotNull(lastHistoryId);

        // Pass non-dictionary content
        await service.TerminerEtape(lastHistoryId!.Value, "just a string");

        // Original variable preserved, no new variables added
        Assert.Single(capturedVariables);
        Assert.Equal("value", capturedVariables["key"]);
    }

    #endregion

    #region Content-Driven Decision Routing

    [Fact]
    public async Task ContentDrivenDecision_RoutesBasedOnVariableValue()
    {
        var executedNodes = new List<string>();
        var businessHandler = CreateBusinessHandler(inst =>
        {
            // no-op: we just track which nodes execute via history
        });

        // Business handler that tracks node names
        var trackingHandler = Substitute.For<INodeHandler>();
        trackingHandler.NodeType.Returns(NodeType.Business);
        trackingHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                executedNodes.Add(node.Name);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var def = new ProcessDefinition("ApprovalFlow", "1.0");

        var decision = new DecisionNode { Name = "CheckAmount", DisplayName = "Check Amount" };
        decision.AddCondition("amount", 1000.0, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "ManagerApproval");
        decision.SetNoeudParDefaut("AutoApprove");

        var managerNode = new BusinessNode("NotifyManager") { Name = "ManagerApproval", DisplayName = "Manager Approval" };
        var autoNode = new BusinessNode("AutoProcess") { Name = "AutoApprove", DisplayName = "Auto Approve" };

        def.AddNode(decision);
        def.AddNode(managerNode);
        def.AddNode(autoNode);
        def.StartNodeId = "CheckAmount";

        var decisionHandler = new DecisionNodeHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new INodeHandler[] { trackingHandler, decisionHandler });

        // Test high-value route
        var highInstance = new ProcessInstance(1) { DefinitionName = "ApprovalFlow" };
        highInstance.Variables["amount"] = 1500.0;
        await engine.ExecuteAsync(highInstance);

        Assert.Contains("ManagerApproval", executedNodes);
        Assert.DoesNotContain("AutoApprove", executedNodes);

        // Test low-value route
        executedNodes.Clear();
        var lowInstance = new ProcessInstance(2) { DefinitionName = "ApprovalFlow" };
        lowInstance.Variables["amount"] = 500.0;
        await engine.ExecuteAsync(lowInstance);

        Assert.Contains("AutoApprove", executedNodes);
        Assert.DoesNotContain("ManagerApproval", executedNodes);
    }

    [Fact]
    public async Task ContentDrivenDecision_MultipleConditions_FirstMatchWins()
    {
        var executedNodes = new List<string>();
        var trackingHandler = Substitute.For<INodeHandler>();
        trackingHandler.NodeType.Returns(NodeType.Business);
        trackingHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                executedNodes.Add(node.Name);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var def = new ProcessDefinition("PriorityRouting", "1.0");

        var decision = new DecisionNode { Name = "Route", DisplayName = "Route" };
        decision.AddCondition("priority", "critical", OperateurFiltre.Egal, TypeDonnee.Texte, "CriticalHandler");
        decision.AddCondition("priority", "high", OperateurFiltre.Egal, TypeDonnee.Texte, "HighHandler");
        decision.SetNoeudParDefaut("NormalHandler");

        def.AddNode(decision);
        def.AddNode(new BusinessNode("HandleCritical") { Name = "CriticalHandler", DisplayName = "Critical" });
        def.AddNode(new BusinessNode("HandleHigh") { Name = "HighHandler", DisplayName = "High" });
        def.AddNode(new BusinessNode("HandleNormal") { Name = "NormalHandler", DisplayName = "Normal" });
        def.StartNodeId = "Route";

        var engine = new FlowEngine(new[] { def },
            handlers: new INodeHandler[] { trackingHandler, new DecisionNodeHandler() });

        var instance = new ProcessInstance(1) { DefinitionName = "PriorityRouting" };
        instance.Variables["priority"] = "critical";
        await engine.ExecuteAsync(instance);

        Assert.Single(executedNodes);
        Assert.Equal("CriticalHandler", executedNodes[0]);
    }

    [Fact]
    public async Task ContentDrivenDecision_MissingVariable_FallsToDefault()
    {
        var def = new ProcessDefinition("MissingVarFlow", "1.0");

        var decision = new DecisionNode { Name = "Check", DisplayName = "Check" };
        decision.AddCondition("category", "VIP", OperateurFiltre.Egal, TypeDonnee.Texte, "VIPPath");
        decision.SetNoeudParDefaut("StandardPath");

        var vipNode = new BusinessNode("VIPProcess") { Name = "VIPPath", DisplayName = "VIP" };
        var stdNode = new BusinessNode("StdProcess") { Name = "StandardPath", DisplayName = "Standard" };

        def.AddNode(decision);
        def.AddNode(vipNode);
        def.AddNode(stdNode);
        def.StartNodeId = "Check";

        var businessHandler = CreateBusinessHandler();
        var engine = new FlowEngine(new[] { def },
            handlers: new INodeHandler[] { businessHandler, new DecisionNodeHandler() });

        // No "category" variable set at all
        var instance = new ProcessInstance(1) { DefinitionName = "MissingVarFlow" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.True(result.ExecutionHistory.Any(h => h.NodeId == "StandardPath"));
        Assert.False(result.ExecutionHistory.Any(h => h.NodeId == "VIPPath"));
    }

    #endregion

    #region Content Propagation Through Subprocesses

    [Fact]
    public async Task SubProcess_InputOutputMapping_PropagatesContentCorrectly()
    {
        var repository = CreateInMemoryRepository();

        // SubProcess business handler that transforms variables
        var subBusinessHandler = Substitute.For<INodeHandler>();
        subBusinessHandler.NodeType.Returns(NodeType.Business);
        subBusinessHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);

                // Simulate business logic: calculate tax
                if (inst.Variables.TryGetValue("orderAmount", out var amount) && amount is double amt)
                {
                    inst.Variables["calculatedTax"] = amt * 0.15;
                    inst.Variables["totalWithTax"] = amt * 1.15;
                }

                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        // Sub process definition
        var subDef = new ProcessDefinition("TaxCalculation", "1.0");
        subDef.AddNode(new BusinessNode("CalcTax") { Name = "CalcTax", DisplayName = "Calculate Tax" });

        // Parent definition
        var parentDef = new ProcessDefinition("OrderProcess", "1.0");
        var initNode = new BusinessNode("InitOrder") { Name = "InitOrder", DisplayName = "Init" };
        var subNode = new SubProcessNode(subDef)
        {
            Name = "RunTaxCalc",
            DisplayName = "Run Tax Calculation",
            InputMapping = new Dictionary<string, string>
            {
                ["parentAmount"] = "orderAmount"
            },
            OutputMapping = new Dictionary<string, string>
            {
                ["calculatedTax"] = "orderTax",
                ["totalWithTax"] = "orderTotal"
            }
        };
        var finalNode = new BusinessNode("Finalize") { Name = "Finalize", DisplayName = "Finalize" };

        initNode.NextNodeIds.Add("RunTaxCalc");
        subNode.NextNodeIds.Add("Finalize");
        parentDef.AddNode(initNode);
        parentDef.AddNode(subNode);
        parentDef.AddNode(finalNode);

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.Business] = subBusinessHandler
        };
        var subProcessHandler = new SubProcessNodeHandler(repository, handlers);
        var engine = new FlowEngine(new[] { parentDef },
            repository,
            new INodeHandler[] { subBusinessHandler, subProcessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "OrderProcess" };
        instance.Variables["parentAmount"] = 100.0;
        instance.Variables["orderId"] = "ORD-001";

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        // Output mapping should have copied results back
        Assert.Equal(15.0, result.Variables["orderTax"]);
        Assert.Equal(115.0, result.Variables["orderTotal"]);
        // Original parent variables remain
        Assert.Equal(100.0, result.Variables["parentAmount"]);
        Assert.Equal("ORD-001", result.Variables["orderId"]);
    }

    [Fact]
    public async Task SubProcess_ContentIsolation_ChildDoesNotPollutParent()
    {
        var repository = CreateInMemoryRepository();

        var businessHandler = Substitute.For<INodeHandler>();
        businessHandler.NodeType.Returns(NodeType.Business);
        businessHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);
                // Subprocess adds internal variables
                inst.Variables["internalTemp"] = "should_not_leak";
                inst.Variables["debugInfo"] = "internal_only";
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var subDef = new ProcessDefinition("SubProcess", "1.0");
        subDef.AddNode(new BusinessNode("SubWork") { Name = "SubWork", DisplayName = "Sub Work" });

        var parentDef = new ProcessDefinition("ParentProcess", "1.0");
        var subNode = new SubProcessNode(subDef)
        {
            Name = "RunSub",
            DisplayName = "Run Sub",
            // Only map specific output, not everything
            OutputMapping = new Dictionary<string, string>()
        };
        parentDef.AddNode(subNode);

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.Business] = businessHandler
        };
        var subHandler = new SubProcessNodeHandler(repository, handlers);
        var engine = new FlowEngine(new[] { parentDef }, repository,
            new INodeHandler[] { businessHandler, subHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "ParentProcess" };
        instance.Variables["parentOnly"] = "preserved";
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal("preserved", result.Variables["parentOnly"]);
        // Internal subprocess variables should NOT leak to parent
        Assert.False(result.Variables.ContainsKey("internalTemp"));
        Assert.False(result.Variables.ContainsKey("debugInfo"));
    }

    #endregion

    #region Multi-Step Workflow With Content Accumulation

    [Fact]
    public async Task MultiStepWorkflow_ContentAccumulatesAcrossSteps()
    {
        // Simulates a multi-step form wizard where each step adds variables
        var repository = CreateInMemoryRepository();

        var businessHandler = CreateBusinessHandler();

        var def = new ProcessDefinition("Wizard", "1.0");
        var step1 = new InteractiveNode { Name = "PersonalInfo", DisplayName = "Personal Info" };
        var step2 = new InteractiveNode { Name = "AddressInfo", DisplayName = "Address Info" };
        var step3 = new InteractiveNode { Name = "PaymentInfo", DisplayName = "Payment Info" };
        var finalStep = new BusinessNode("SubmitOrder") { Name = "Submit", DisplayName = "Submit" };

        step1.NextNodeIds.Add("AddressInfo");
        step2.NextNodeIds.Add("PaymentInfo");
        step3.NextNodeIds.Add("Submit");
        def.AddNode(step1);
        def.AddNode(step2);
        def.AddNode(step3);
        def.AddNode(finalStep);

        var engine = new FlowEngine(new[] { def }, repository, new[] { businessHandler });

        // Step 1: Start process
        var instance = new ProcessInstance(1) { DefinitionName = "Wizard" };
        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // Step 2: User provides personal info
        instance.Variables["firstName"] = "Alice";
        instance.Variables["lastName"] = "Smith";
        instance = await engine.ContinueAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // Step 3: User provides address
        instance.Variables["city"] = "Montreal";
        instance.Variables["postalCode"] = "H2X 1Y4";
        instance = await engine.ContinueAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // Step 4: User provides payment
        instance.Variables["cardType"] = "Visa";
        instance.Variables["lastFour"] = "1234";
        instance = await engine.ContinueAsync(instance);

        // All steps complete - verify all content accumulated
        Assert.Equal(ProcessStatus.Completed, instance.Status);
        Assert.Equal("Alice", instance.Variables["firstName"]);
        Assert.Equal("Smith", instance.Variables["lastName"]);
        Assert.Equal("Montreal", instance.Variables["city"]);
        Assert.Equal("H2X 1Y4", instance.Variables["postalCode"]);
        Assert.Equal("Visa", instance.Variables["cardType"]);
        Assert.Equal("1234", instance.Variables["lastFour"]);
    }

    [Fact]
    public async Task SignalBasedWorkflow_ContentPreservedAcrossSignalWait()
    {
        var def = new ProcessDefinition("ApprovalWithSignal", "1.0");

        var init = new BusinessNode("Init") { Name = "Init", DisplayName = "Init" };
        var waitApproval = new WaitForSignalNode("manager-approval")
        {
            Name = "WaitApproval",
            DisplayName = "Wait For Approval"
        };
        var postApproval = new BusinessNode("PostApproval") { Name = "PostApproval", DisplayName = "Post Approval" };

        init.NextNodeIds.Add("WaitApproval");
        waitApproval.NextNodeIds.Add("PostApproval");
        def.AddNode(init);
        def.AddNode(waitApproval);
        def.AddNode(postApproval);

        var businessHandler = CreateBusinessHandler();
        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "ApprovalWithSignal" };
        instance.Variables["requestId"] = "REQ-100";
        instance.Variables["requestedBy"] = "Bob";
        instance.Variables["amount"] = 5000.0;

        // Execute until signal wait
        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingSignal, instance.Status);

        // Verify content persists during wait
        Assert.Equal("REQ-100", instance.Variables["requestId"]);
        Assert.Equal("Bob", instance.Variables["requestedBy"]);
        Assert.Equal(5000.0, instance.Variables["amount"]);

        // Send signal - content should still be there after completion
        instance = await engine.SignalAsync(instance, "manager-approval");
        Assert.Equal(ProcessStatus.Completed, instance.Status);
        Assert.Equal("REQ-100", instance.Variables["requestId"]);
        Assert.Equal("Bob", instance.Variables["requestedBy"]);
        Assert.Equal(5000.0, instance.Variables["amount"]);
    }

    #endregion

    #region Content With Business Node Parameters

    [Fact]
    public async Task BusinessNode_ReceivesParametersAndVariables()
    {
        Dictionary<string, object>? receivedParams = null;
        long receivedProcessId = 0;

        var executor = Substitute.For<ICommandExecutor>();
        executor.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())
            .Returns(callInfo =>
            {
                receivedProcessId = callInfo.ArgAt<long>(1);
                receivedParams = callInfo.ArgAt<Dictionary<string, object>?>(3);
                return Task.CompletedTask;
            });

        var businessHandler = new BusinessNodeHandler(executor);

        var def = new ProcessDefinition("ParamTest", "1.0");
        var node = new BusinessNode("SendEmail")
        {
            Name = "SendEmail",
            DisplayName = "Send Email",
            Parameters = new Dictionary<string, object>
            {
                ["template"] = "welcome",
                ["retryCount"] = 3
            }
        };
        def.AddNode(node);

        var engine = new FlowEngine(new[] { def }, handlers: new INodeHandler[] { businessHandler });

        var instance = new ProcessInstance(42) { DefinitionName = "ParamTest" };
        instance.Variables["recipientEmail"] = "alice@example.com";
        instance.Variables["userName"] = "Alice";

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal(42L, receivedProcessId);
        Assert.NotNull(receivedParams);
        Assert.Equal("welcome", receivedParams!["template"]);
        Assert.Equal(3, receivedParams["retryCount"]);
    }

    #endregion

    #region Content With FlowService End-to-End

    [Fact]
    public async Task FlowService_CreateWithVariables_VariablesAvailableInProcess()
    {
        var repository = CreateInMemoryRepository();
        var capturedVariables = new Dictionary<string, object>();

        var businessHandler = CreateBusinessHandler(inst =>
        {
            foreach (var kvp in inst.Variables)
                capturedVariables[kvp.Key] = kvp.Value;
        });

        var def = new ProcessDefinition("SimpleFlow", "1.0");
        def.AddNode(new BusinessNode("Process") { Name = "Process", DisplayName = "Process" });

        var service = new FlowService(new[] { def }, repository, new[] { businessHandler });

        var processId = await service.CreateProcessInstance("SimpleFlow",
            new Dictionary<string, object>
            {
                ["orderId"] = "ORD-999",
                ["amount"] = 42.5,
                ["express"] = true
            });

        Assert.Equal("ORD-999", capturedVariables["orderId"]);
        Assert.Equal(42.5, capturedVariables["amount"]);
        Assert.Equal(true, capturedVariables["express"]);
    }

    [Fact]
    public async Task FlowService_ObtenirAsync_ReturnsProcessWithVariables()
    {
        var repository = CreateInMemoryRepository();
        var businessHandler = CreateBusinessHandler();

        var def = new ProcessDefinition("QueryFlow", "1.0");
        var interactive = new InteractiveNode { Name = "Wait", DisplayName = "Wait" };
        def.AddNode(interactive);

        var service = new FlowService(new[] { def }, repository, new[] { businessHandler });

        var processId = await service.CreateProcessInstance("QueryFlow",
            new Dictionary<string, object>
            {
                ["clientId"] = "CLI-100",
                ["type"] = "premium"
            });

        var processus = await service.ObtenirAsync(processId);

        Assert.Equal("CLI-100", processus.Variables["clientId"]);
        Assert.Equal("premium", processus.Variables["type"]);
        Assert.Equal(ProcessStatus.WaitingInteraction, processus.Status);
        Assert.Equal("QueryFlow", processus.DefinitionName);
    }

    [Fact]
    public async Task FlowService_RechercherParVariable_FindsProcessByContent()
    {
        var repository = CreateInMemoryRepository();
        var businessHandler = CreateBusinessHandler();

        var def = new ProcessDefinition("SearchFlow", "1.0");
        var interactive = new InteractiveNode { Name = "Wait", DisplayName = "Wait" };
        def.AddNode(interactive);

        var service = new FlowService(new[] { def }, repository, new[] { businessHandler });

        // Create multiple processes with different variables
        await service.CreateProcessInstance("SearchFlow",
            new Dictionary<string, object> { ["department"] = "finance", ["region"] = "east" });
        await service.CreateProcessInstance("SearchFlow",
            new Dictionary<string, object> { ["department"] = "engineering", ["region"] = "west" });
        await service.CreateProcessInstance("SearchFlow",
            new Dictionary<string, object> { ["department"] = "finance", ["region"] = "west" });

        // Search by department
        var results = await service.RechercherParVariable(new List<FiltreVariable>
        {
            new FiltreVariable("department", "finance")
        });

        Assert.Equal(2, results.Count);
        Assert.All(results, p => Assert.Equal("finance", p.Variables["department"]));
    }

    #endregion

    #region Content Through Complete Lifecycle

    [Fact]
    public async Task CompleteLifecycle_ContentFlowsThroughEntireProcess()
    {
        // Full lifecycle: create → interactive → decision → business → complete
        var repository = CreateInMemoryRepository();
        var finalVariables = new Dictionary<string, object>();

        var businessHandler = Substitute.For<INodeHandler>();
        businessHandler.NodeType.Returns(NodeType.Business);
        businessHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);
                if (node.Name == "FinalProcess")
                {
                    foreach (var kvp in inst.Variables)
                        finalVariables[kvp.Key] = kvp.Value;
                }
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var def = new ProcessDefinition("FullLifecycle", "1.0");

        // Step 1: Interactive - collect user data
        var collectData = new InteractiveNode { Name = "CollectData", DisplayName = "Collect Data" };

        // Step 2: Decision - route based on user's choice
        var decision = new DecisionNode { Name = "RouteDecision", DisplayName = "Route" };
        decision.AddCondition("requestType", "urgent", OperateurFiltre.Egal, TypeDonnee.Texte, "UrgentPath");
        decision.SetNoeudParDefaut("NormalPath");

        // Step 3a: Urgent path
        var urgentNode = new BusinessNode("HandleUrgent")
        {
            Name = "UrgentPath",
            DisplayName = "Urgent Processing"
        };

        // Step 3b: Normal path
        var normalNode = new BusinessNode("HandleNormal")
        {
            Name = "NormalPath",
            DisplayName = "Normal Processing"
        };

        // Step 4: Final processing (both paths converge)
        var finalNode = new BusinessNode("Finalize")
        {
            Name = "FinalProcess",
            DisplayName = "Final Process"
        };

        collectData.NextNodeIds.Add("RouteDecision");
        urgentNode.NextNodeIds.Add("FinalProcess");
        normalNode.NextNodeIds.Add("FinalProcess");

        def.AddNode(collectData);
        def.AddNode(decision);
        def.AddNode(urgentNode);
        def.AddNode(normalNode);
        def.AddNode(finalNode);

        var decisionHandler = new DecisionNodeHandler();
        var engine = new FlowEngine(new[] { def }, repository,
            new INodeHandler[] { businessHandler, decisionHandler });

        // Create with initial variables
        var instance = new ProcessInstance(1) { DefinitionName = "FullLifecycle" };
        instance.Variables["createdBy"] = "system";
        instance.Variables["timestamp"] = "2024-01-01";

        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // User submits form data (via TerminerEtape-like variable merge)
        instance.Variables["requestType"] = "urgent";
        instance.Variables["description"] = "Server down";
        instance.Variables["severity"] = 1;

        instance = await engine.ContinueAsync(instance);

        // Verify final node received all accumulated content
        Assert.Equal(ProcessStatus.Completed, instance.Status);
        Assert.Equal("system", finalVariables["createdBy"]);
        Assert.Equal("2024-01-01", finalVariables["timestamp"]);
        Assert.Equal("urgent", finalVariables["requestType"]);
        Assert.Equal("Server down", finalVariables["description"]);
        Assert.Equal(1, finalVariables["severity"]);

        // Verify the urgent path was taken
        Assert.True(instance.ExecutionHistory.Any(h => h.NodeId == "UrgentPath"));
        Assert.False(instance.ExecutionHistory.Any(h => h.NodeId == "NormalPath"));
    }

    [Fact]
    public async Task CompleteLifecycle_MultipleInteractiveSteps_WithDecisionBranching()
    {
        var repository = CreateInMemoryRepository();
        var businessHandler = CreateBusinessHandler();
        var decisionHandler = new DecisionNodeHandler();

        var def = new ProcessDefinition("LoanApplication", "1.0");

        // Step 1: Collect application
        var collectApp = new InteractiveNode { Name = "CollectApplication", DisplayName = "Collect Application" };

        // Step 2: Decision on income
        var incomeCheck = new DecisionNode { Name = "IncomeCheck", DisplayName = "Income Check" };
        incomeCheck.AddCondition("annualIncome", 50000.0, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "CollectDocuments");
        incomeCheck.SetNoeudParDefaut("Rejected");

        // Step 3a: Collect documents (high income path)
        var collectDocs = new InteractiveNode { Name = "CollectDocuments", DisplayName = "Collect Documents" };

        // Step 3b: Rejected
        var rejected = new BusinessNode("RejectApp") { Name = "Rejected", DisplayName = "Rejected" };

        // Step 4: Final approval
        var approved = new BusinessNode("ApproveApp") { Name = "Approved", DisplayName = "Approved" };

        collectApp.NextNodeIds.Add("IncomeCheck");
        collectDocs.NextNodeIds.Add("Approved");

        def.AddNode(collectApp);
        def.AddNode(incomeCheck);
        def.AddNode(collectDocs);
        def.AddNode(rejected);
        def.AddNode(approved);

        var engine = new FlowEngine(new[] { def }, repository,
            new INodeHandler[] { businessHandler, decisionHandler });

        // Start application
        var instance = new ProcessInstance(1) { DefinitionName = "LoanApplication" };
        instance.Variables["applicantName"] = "Alice";
        instance = await engine.ExecuteAsync(instance);
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // User provides income info
        instance.Variables["annualIncome"] = 75000.0;
        instance.Variables["employmentYears"] = 5;
        instance = await engine.ContinueAsync(instance);

        // Should route to CollectDocuments (income >= 50000)
        Assert.Equal(ProcessStatus.WaitingInteraction, instance.Status);

        // User provides documents
        instance.Variables["documentIds"] = "DOC-001,DOC-002";
        instance.Variables["verified"] = true;
        instance = await engine.ContinueAsync(instance);

        Assert.Equal(ProcessStatus.Completed, instance.Status);
        Assert.Equal("Alice", instance.Variables["applicantName"]);
        Assert.Equal(75000.0, instance.Variables["annualIncome"]);
        Assert.Equal("DOC-001,DOC-002", instance.Variables["documentIds"]);
        Assert.True(instance.ExecutionHistory.Any(h => h.NodeId == "Approved"));
    }

    #endregion

    #region Content Type Handling

    [Fact]
    public async Task VariousContentTypes_HandledCorrectlyInWorkflow()
    {
        var capturedVariables = new Dictionary<string, object>();

        var businessHandler = CreateBusinessHandler(inst =>
        {
            foreach (var kvp in inst.Variables)
                capturedVariables[kvp.Key] = kvp.Value;
        });

        var def = new ProcessDefinition("TypeTest", "1.0");
        def.AddNode(new BusinessNode("Check") { Name = "Check", DisplayName = "Check" });

        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "TypeTest" };
        instance.Variables["stringVal"] = "hello";
        instance.Variables["intVal"] = 42;
        instance.Variables["doubleVal"] = 3.14;
        instance.Variables["boolVal"] = true;
        instance.Variables["dateVal"] = new DateTime(2024, 6, 15);
        instance.Variables["listVal"] = new List<string> { "a", "b", "c" };
        instance.Variables["nestedDict"] = new Dictionary<string, object>
        {
            ["inner"] = "value",
            ["count"] = 10
        };

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal("hello", capturedVariables["stringVal"]);
        Assert.Equal(42, capturedVariables["intVal"]);
        Assert.Equal(3.14, capturedVariables["doubleVal"]);
        Assert.Equal(true, capturedVariables["boolVal"]);
        Assert.Equal(new DateTime(2024, 6, 15), capturedVariables["dateVal"]);
        Assert.IsType<List<string>>(capturedVariables["listVal"]);
        Assert.IsType<Dictionary<string, object>>(capturedVariables["nestedDict"]);
    }

    #endregion

    #region Execution History Content Tracking

    [Fact]
    public async Task ExecutionHistory_TracksContentFlowThroughNodes()
    {
        var businessHandler = CreateBusinessHandler();
        var decisionHandler = new DecisionNodeHandler();

        var def = new ProcessDefinition("HistoryTracking", "1.0");
        var start = new BusinessNode("StartCmd") { Name = "Start", DisplayName = "Start Step" };
        var decision = new DecisionNode { Name = "Route", DisplayName = "Route Decision" };
        decision.AddCondition("flag", true, OperateurFiltre.Egal, TypeDonnee.Booleen, "PathA");
        decision.SetNoeudParDefaut("PathB");

        var pathA = new BusinessNode("DoA") { Name = "PathA", DisplayName = "Path A" };
        var pathB = new BusinessNode("DoB") { Name = "PathB", DisplayName = "Path B" };
        var end = new BusinessNode("EndCmd") { Name = "End", DisplayName = "End Step" };

        start.NextNodeIds.Add("Route");
        pathA.NextNodeIds.Add("End");
        pathB.NextNodeIds.Add("End");

        def.AddNode(start);
        def.AddNode(decision);
        def.AddNode(pathA);
        def.AddNode(pathB);
        def.AddNode(end);

        var engine = new FlowEngine(new[] { def },
            handlers: new INodeHandler[] { businessHandler, decisionHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "HistoryTracking" };
        instance.Variables["flag"] = true;

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);

        // Verify execution history captured the correct path
        var nodeNames = result.ExecutionHistory.Select(h => h.NodeId).ToList();
        Assert.Equal(new[] { "Start", "Route", "PathA", "End" }, nodeNames);
        Assert.All(result.ExecutionHistory, h => Assert.True(h.Success));
        Assert.Equal(4, result.CompletedStepsCount);
        Assert.Equal(0, result.FailedStepsCount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EmptyVariables_ProcessCompletesNormally()
    {
        var businessHandler = CreateBusinessHandler();
        var def = new ProcessDefinition("EmptyVars", "1.0");
        def.AddNode(new BusinessNode("Step") { Name = "Step", DisplayName = "Step" });

        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });
        var instance = new ProcessInstance(1) { DefinitionName = "EmptyVars" };

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Empty(result.Variables);
    }

    [Fact]
    public async Task NullVariableValues_HandledGracefully()
    {
        var capturedVariables = new Dictionary<string, object?>();
        var businessHandler = Substitute.For<INodeHandler>();
        businessHandler.NodeType.Returns(NodeType.Business);
        businessHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);
                foreach (var kvp in inst.Variables)
                    capturedVariables[kvp.Key] = kvp.Value;
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var def = new ProcessDefinition("NullVars", "1.0");
        def.AddNode(new BusinessNode("Step") { Name = "Step", DisplayName = "Step" });

        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });
        var instance = new ProcessInstance(1) { DefinitionName = "NullVars" };
        instance.Variables["nullableField"] = null!;
        instance.Variables["normalField"] = "exists";

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Null(capturedVariables["nullableField"]);
        Assert.Equal("exists", capturedVariables["normalField"]);
    }

    [Fact]
    public async Task LargeVariableSet_HandledCorrectly()
    {
        var capturedCount = 0;
        var businessHandler = CreateBusinessHandler(inst =>
        {
            capturedCount = inst.Variables.Count;
        });

        var def = new ProcessDefinition("LargeVars", "1.0");
        def.AddNode(new BusinessNode("Step") { Name = "Step", DisplayName = "Step" });

        var engine = new FlowEngine(new[] { def }, handlers: new[] { businessHandler });
        var instance = new ProcessInstance(1) { DefinitionName = "LargeVars" };

        for (int i = 0; i < 100; i++)
        {
            instance.Variables[$"var_{i}"] = $"value_{i}";
        }

        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal(100, capturedCount);
        Assert.Equal(100, result.Variables.Count);
    }

    #endregion
}
