using SimpleBPM;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;
using NSubstitute;

namespace SimpleBPM.Tests;

/// <summary>
/// High-level integration tests covering the complete FlowService public API end-to-end.
/// Each test drives a realistic business scenario through creation, interaction, signalling,
/// and completion, verifying observable state exclusively via IFlowService methods.
/// An in-memory repository replaces Oracle so no external infrastructure is required.
/// </summary>
public class WorkflowIntegrationTests
{
    #region Infrastructure

    /// <summary>Creates a business node handler that optionally records which nodes it executed.</summary>
    private static INodeHandler CreateBusinessHandler(Action<string, ProcessInstance>? onExecute = null)
    {
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<NodeDefinition>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);
                onExecute?.Invoke(node.Name, inst);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeName = node.NextNodeIds.FirstOrDefault()
                });
            });
        return handler;
    }

    private static InMemoryProcessRepository CreateRepository() => new();

    /// <summary>
    /// Builds a FlowService backed by the given in-memory repository.
    /// A DecisionNodeHandler is always included; pass additional handlers as needed.
    /// </summary>
    private static FlowService CreateService(
        InMemoryProcessRepository repository,
        IEnumerable<ProcessDefinition> definitions,
        params INodeHandler[] additionalHandlers)
    {
        var handlers = new List<INodeHandler>(additionalHandlers) { new DecisionNodeHandler() };
        return new FlowService(definitions, repository, handlers);
    }

    /// <summary>
    /// Minimal in-memory IProcessRepository for integration testing without Oracle.
    /// Assigns auto-incrementing IDs to both process instances and node history entries.
    /// </summary>
    internal sealed class InMemoryProcessRepository : IProcessRepository
    {
        private long _sequence;
        private long _historySequence;
        private readonly Dictionary<long, ProcessInstance> _instances = new();
        private readonly Dictionary<long, (NodeInstance History, long ProcessId)> _historyEntries = new();

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
            var results = _instances.Values
                .Where(inst => filtres.All(f =>
                    inst.Variables.TryGetValue(f.NomVariable, out var val) && f.Correspond(val)))
                .ToList();
            return Task.FromResult(results);
        }

        public Task<(NodeInstance History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId) =>
            Task.FromResult(_historyEntries.TryGetValue(historyId, out var entry)
                ? (entry.History, entry.ProcessId)
                : ((NodeInstance History, long ProcessId)?)null);

        public Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeName) =>
            Task.FromResult(_instances.Values.FirstOrDefault(i =>
                i.ParentProcessId == parentProcessId && i.ParentNodeName == parentNodeName));

        public Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId) =>
            Task.FromResult(_instances.Values
                .Where(i => i.ParentProcessId == parentProcessId)
                .ToList());

        /// <summary>Returns the ID of the most recently recorded history entry for a process.</summary>
        public long? GetLastHistoryId(long processId)
        {
            var keys = _historyEntries
                .Where(kvp => kvp.Value.ProcessId == processId)
                .Select(kvp => kvp.Key)
                .ToList();
            return keys.Count > 0 ? keys.Max() : null;
        }

        private void TrackNewHistoryEntries(ProcessInstance instance)
        {
            var tracked = _historyEntries.Values
                .Where(e => e.ProcessId == instance.ProcessId)
                .Select(e => e.History)
                .ToHashSet();
            foreach (var entry in instance.ExecutionHistory.Where(e => !tracked.Contains(e)))
                _historyEntries[Interlocked.Increment(ref _historySequence)] = (entry, instance.ProcessId);
        }
    }

    #endregion

    // -------------------------------------------------------------------------
    // Decision routing (straight-through – no interactive nodes required)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExpenseApproval_HighAmount_RoutedToManagerApprovalAndCompletes()
    {
        // Arrange
        var repo = CreateRepository();
        var executedNodes = new List<string>();
        var handler = CreateBusinessHandler((name, _) => executedNodes.Add(name));

        var def = new ProcessDefinition("ExpenseApproval", "1.0");
        var decision = new DecisionNode { Name = "AmountCheck", DisplayName = "Amount Check" };
        decision.AddCondition("amount", 1000.0, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "ManagerApproval");
        decision.SetNoeudParDefaut("AutoApproved");

        def.AddNode(decision);
        def.AddNode(new BusinessNode("Mgr") { Name = "ManagerApproval", DisplayName = "Manager Approval" });
        def.AddNode(new BusinessNode("Auto") { Name = "AutoApproved", DisplayName = "Auto Approved" });
        def.StartNodeId = "AmountCheck";

        var service = CreateService(repo, new[] { def }, handler);

        // Act: submit a high-value expense
        var processId = await service.CreateProcessInstanceAsync("ExpenseApproval",
            new Dictionary<string, object> { ["amount"] = 1500.0, ["submittedBy"] = "emp-42" });

        // Assert: routed to manager approval; original variables preserved
        var process = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, process.Status);
        Assert.Equal(1500.0, process.Variables["amount"]);
        Assert.Equal("emp-42", process.Variables["submittedBy"]);
        Assert.Contains("ManagerApproval", executedNodes);
        Assert.DoesNotContain("AutoApproved", executedNodes);
    }

    [Fact]
    public async Task ExpenseApproval_LowAmount_RoutedToAutoApprovalAndCompletes()
    {
        // Arrange
        var repo = CreateRepository();
        var executedNodes = new List<string>();
        var handler = CreateBusinessHandler((name, _) => executedNodes.Add(name));

        var def = new ProcessDefinition("ExpenseApproval", "1.0");
        var decision = new DecisionNode { Name = "AmountCheck", DisplayName = "Amount Check" };
        decision.AddCondition("amount", 1000.0, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "ManagerApproval");
        decision.SetNoeudParDefaut("AutoApproved");

        def.AddNode(decision);
        def.AddNode(new BusinessNode("Mgr") { Name = "ManagerApproval", DisplayName = "Manager Approval" });
        def.AddNode(new BusinessNode("Auto") { Name = "AutoApproved", DisplayName = "Auto Approved" });
        def.StartNodeId = "AmountCheck";

        var service = CreateService(repo, new[] { def }, handler);

        // Act: submit a low-value expense
        var processId = await service.CreateProcessInstanceAsync("ExpenseApproval",
            new Dictionary<string, object> { ["amount"] = 250.0, ["submittedBy"] = "emp-7" });

        // Assert: auto-approved without manager involvement
        var process = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, process.Status);
        Assert.Equal(250.0, process.Variables["amount"]);
        Assert.Contains("AutoApproved", executedNodes);
        Assert.DoesNotContain("ManagerApproval", executedNodes);
    }

    // -------------------------------------------------------------------------
    // Interactive process lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ServiceRequest_Create_PausesForUserInputWithInitialVariables()
    {
        // Arrange: process with a single interactive step
        var repo = CreateRepository();
        var def = new ProcessDefinition("ServiceRequest", "1.0");
        var form = new InteractiveNode { Name = "CollectDetails", DisplayName = "Collect Details" };
        var done = new BusinessNode("Done") { Name = "Done", DisplayName = "Done" };
        form.NextNodeIds.Add("Done");
        def.AddNode(form);
        def.AddNode(done);

        var service = CreateService(repo, new[] { def });

        // Act
        var processId = await service.CreateProcessInstanceAsync("ServiceRequest",
            new Dictionary<string, object>
            {
                ["requesterId"] = "EMP-001",
                ["requestType"] = "hardware",
                ["priority"] = "high"
            });

        // Assert: process waits for user input and retains initial variables
        var process = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.WaitingInteraction, process.Status);
        Assert.Equal("ServiceRequest", process.DefinitionName);
        Assert.Equal("EMP-001", process.Variables["requesterId"]);
        Assert.Equal("hardware", process.Variables["requestType"]);
        Assert.Equal("high", process.Variables["priority"]);
    }

    [Fact]
    public async Task ServiceRequest_TerminerEtape_MergesFormDataAndAdvancesProcess()
    {
        // Arrange: interactive process; TerminerEtape is called with the node-history ID
        var repo = CreateRepository();
        var def = new ProcessDefinition("ServiceRequest", "1.0");
        var form = new InteractiveNode { Name = "CollectDetails", DisplayName = "Collect Details" };
        var done = new BusinessNode("Done") { Name = "Done", DisplayName = "Done" };
        form.NextNodeIds.Add("Done");
        def.AddNode(form);
        def.AddNode(done);

        var service = CreateService(repo, new[] { def });
        var processId = await service.CreateProcessInstanceAsync("ServiceRequest",
            new Dictionary<string, object> { ["requesterId"] = "EMP-002" });

        Assert.Equal(ProcessStatus.WaitingInteraction, (await service.ObtenirAsync(processId)).Status);

        // Act: user submits the form; content is merged into process variables
        var historyId = repo.GetLastHistoryId(processId)!.Value;
        await service.TerminerEtapeAsync(historyId, new Dictionary<string, object>
        {
            ["description"] = "Need a new laptop",
            ["urgency"] = "high",
            ["estimatedCost"] = 1200.0
        });

        // Assert: initial variable preserved, form data merged, process advanced
        var completed = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, completed.Status);
        Assert.Equal("EMP-002", completed.Variables["requesterId"]);
        Assert.Equal("Need a new laptop", completed.Variables["description"]);
        Assert.Equal("high", completed.Variables["urgency"]);
        Assert.Equal(1200.0, completed.Variables["estimatedCost"]);
    }

    [Fact]
    public async Task ServiceRequest_TerminerEtapeEnCours_MergesFormDataByProcessId()
    {
        // Arrange: same setup but the process-ID overload is used instead of the history-ID one
        var repo = CreateRepository();
        var def = new ProcessDefinition("ServiceRequest", "1.0");
        var form = new InteractiveNode { Name = "CollectDetails", DisplayName = "Collect Details" };
        var done = new BusinessNode("Done") { Name = "Done", DisplayName = "Done" };
        form.NextNodeIds.Add("Done");
        def.AddNode(form);
        def.AddNode(done);

        var service = CreateService(repo, new[] { def });
        var processId = await service.CreateProcessInstanceAsync("ServiceRequest",
            new Dictionary<string, object> { ["requesterId"] = "EMP-003" });

        // Act: user completes the current step; identified only by process ID
        await service.TerminerEtapeEnCoursAsync(processId, new Dictionary<string, object>
        {
            ["assignedTeam"] = "IT",
            ["ticketNumber"] = "TKT-0042"
        });

        // Assert: variables merged and process completed
        var process = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, process.Status);
        Assert.Equal("EMP-003", process.Variables["requesterId"]);
        Assert.Equal("IT", process.Variables["assignedTeam"]);
        Assert.Equal("TKT-0042", process.Variables["ticketNumber"]);
    }

    [Fact]
    public async Task LoanApplication_TwoPhaseInteraction_VariablesAccumulateAcrossSteps()
    {
        // Arrange: two genuine user interaction phases.
        //
        // Because FlowEngine sets CurrentNodeName to the *next* node after a pause,
        // the node immediately after each interactive step acts as a bridge (its
        // HandleAsync is never called on resume; execution continues from its successor).
        // To produce two pause points the flow uses three interactive nodes:
        //   Phase1 (pauses) → Phase1Bridge (bridge) → Phase2 (pauses) → Complete
        var repo = CreateRepository();
        var def = new ProcessDefinition("LoanApplication", "1.0");

        var phase1 = new InteractiveNode { Name = "Phase1", DisplayName = "Personal Information" };
        var phase1Bridge = new InteractiveNode { Name = "Phase1Bridge", DisplayName = "Phase 1 Bridge" };
        var phase2 = new InteractiveNode { Name = "Phase2", DisplayName = "Financial Information" };
        var complete = new BusinessNode("Complete") { Name = "Complete", DisplayName = "Complete" };

        phase1.NextNodeIds.Add("Phase1Bridge");
        phase1Bridge.NextNodeIds.Add("Phase2");
        phase2.NextNodeIds.Add("Complete");

        def.AddNode(phase1);
        def.AddNode(phase1Bridge);
        def.AddNode(phase2);
        def.AddNode(complete);

        var service = CreateService(repo, new[] { def });

        // Act – Phase 1: applicant provides personal details
        var processId = await service.CreateProcessInstanceAsync("LoanApplication",
            new Dictionary<string, object> { ["applicantId"] = "APP-500" });

        Assert.Equal(ProcessStatus.WaitingInteraction, (await service.ObtenirAsync(processId)).Status);

        await service.TerminerEtapeEnCoursAsync(processId, new Dictionary<string, object>
        {
            ["firstName"] = "Alice",
            ["lastName"] = "Martin",
            ["dateOfBirth"] = "1985-03-12"
        });

        // Process should be paused again at Phase2
        Assert.Equal(ProcessStatus.WaitingInteraction, (await service.ObtenirAsync(processId)).Status);

        // Act – Phase 2: applicant provides financial details
        await service.TerminerEtapeEnCoursAsync(processId, new Dictionary<string, object>
        {
            ["annualIncome"] = 80000.0,
            ["creditScore"] = 720,
            ["requestedAmount"] = 25000.0
        });

        // Assert: all variables from both phases present in the completed process
        var finalProcess = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, finalProcess.Status);
        Assert.Equal("APP-500", finalProcess.Variables["applicantId"]);
        Assert.Equal("Alice", finalProcess.Variables["firstName"]);
        Assert.Equal("Martin", finalProcess.Variables["lastName"]);
        Assert.Equal("1985-03-12", finalProcess.Variables["dateOfBirth"]);
        Assert.Equal(80000.0, finalProcess.Variables["annualIncome"]);
        Assert.Equal(720, finalProcess.Variables["creditScore"]);
        Assert.Equal(25000.0, finalProcess.Variables["requestedAmount"]);
    }

    // -------------------------------------------------------------------------
    // Signal-based workflows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurchaseOrder_WaitsForPaymentSignal_CompletesAfterSignalReceived()
    {
        // Arrange: an order initialises, then waits for a payment-confirmed signal
        var repo = CreateRepository();
        var handler = CreateBusinessHandler();

        var def = new ProcessDefinition("PurchaseOrder", "1.0");
        var initOrder = new BusinessNode("InitOrder") { Name = "InitOrder", DisplayName = "Initialize Order" };
        var waitPayment = new WaitForSignalNode("payment-confirmed")
            { Name = "WaitPayment", DisplayName = "Await Payment" };
        var fulfilOrder = new BusinessNode("FulfilOrder") { Name = "FulfilOrder", DisplayName = "Fulfil Order" };

        initOrder.NextNodeIds.Add("WaitPayment");
        waitPayment.NextNodeIds.Add("FulfilOrder");
        def.AddNode(initOrder);
        def.AddNode(waitPayment);
        def.AddNode(fulfilOrder);

        var service = CreateService(repo, new[] { def }, handler);

        // Act: create order; process should run until the signal wait
        var processId = await service.CreateProcessInstanceAsync("PurchaseOrder",
            new Dictionary<string, object> { ["orderId"] = "PO-999", ["totalAmount"] = 349.99 });

        var waiting = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.WaitingSignal, waiting.Status);
        Assert.Equal("PO-999", waiting.Variables["orderId"]);

        // Send the payment signal
        await service.EnvoyerSignalAsync(processId, "payment-confirmed");

        // Assert: process completes with variables intact
        var completed = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, completed.Status);
        Assert.Equal("PO-999", completed.Variables["orderId"]);
        Assert.Equal(349.99, completed.Variables["totalAmount"]);
    }

    [Fact]
    public async Task ObtenirSignauxEnAttente_WhenProcessWaitsForSignal_ReturnsPendingSignalName()
    {
        // Arrange
        var repo = CreateRepository();
        var def = new ProcessDefinition("ApprovalFlow", "1.0");
        def.AddNode(new WaitForSignalNode("manager-approved")
            { Name = "WaitApproval", DisplayName = "Wait for Approval" });

        var service = CreateService(repo, new[] { def });
        var processId = await service.CreateProcessInstanceAsync("ApprovalFlow");

        Assert.Equal(ProcessStatus.WaitingSignal, (await service.ObtenirAsync(processId)).Status);

        // Act
        var pendingSignals = (await service.ObtenirSignauxEnAttenteAsync(processId)).ToList();

        // Assert: the expected signal name is visible while the process is paused
        Assert.Single(pendingSignals);
        Assert.Equal("manager-approved", pendingSignals[0]);
    }

    // -------------------------------------------------------------------------
    // Variable search
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RechercherParVariable_MultipleDepartments_ReturnsOnlyMatchingProcesses()
    {
        // Arrange: three HR processes across two departments
        var repo = CreateRepository();
        var def = new ProcessDefinition("HRProcess", "1.0");
        var form = new InteractiveNode { Name = "Review", DisplayName = "Review" };
        var done = new BusinessNode("Done") { Name = "Done", DisplayName = "Done" };
        form.NextNodeIds.Add("Done");
        def.AddNode(form);
        def.AddNode(done);

        var service = CreateService(repo, new[] { def });

        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "engineering", ["region"] = "north" });
        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "finance", ["region"] = "east" });
        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "finance", ["region"] = "west" });

        // Act: search for the finance department
        var results = await service.RechercherParVariableAsync(new List<FiltreVariable>
        {
            new FiltreVariable("department", "finance")
        });

        // Assert: exactly the two finance processes are returned
        Assert.Equal(2, results.Count);
        Assert.All(results, p => Assert.Equal("finance", p.Variables["department"]));
        Assert.DoesNotContain(results,
            p => p.Variables.TryGetValue("department", out var d) && d.Equals("engineering"));
    }

    [Fact]
    public async Task RechercherParVariable_MultipleFilters_RequiresAllConditionsToMatch()
    {
        // Arrange
        var repo = CreateRepository();
        var def = new ProcessDefinition("HRProcess", "1.0");
        var form = new InteractiveNode { Name = "Review", DisplayName = "Review" };
        var done = new BusinessNode("Done") { Name = "Done", DisplayName = "Done" };
        form.NextNodeIds.Add("Done");
        def.AddNode(form);
        def.AddNode(done);

        var service = CreateService(repo, new[] { def });

        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "finance", ["region"] = "east" });
        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "finance", ["region"] = "west" });
        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "engineering", ["region"] = "east" });

        // Act: search for finance AND east – only the first process matches both
        var results = await service.RechercherParVariableAsync(new List<FiltreVariable>
        {
            new FiltreVariable("department", "finance"),
            new FiltreVariable("region", "east")
        });

        // Assert
        Assert.Single(results);
        Assert.Equal("finance", results[0].Variables["department"]);
        Assert.Equal("east", results[0].Variables["region"]);
    }

    [Fact]
    public async Task RechercherParVariable_NoMatchingProcesses_ReturnsEmptyList()
    {
        // Arrange
        var repo = CreateRepository();
        var def = new ProcessDefinition("HRProcess", "1.0");
        var form = new InteractiveNode { Name = "Review", DisplayName = "Review" };
        var done = new BusinessNode("Done") { Name = "Done", DisplayName = "Done" };
        form.NextNodeIds.Add("Done");
        def.AddNode(form);
        def.AddNode(done);

        var service = CreateService(repo, new[] { def });
        await service.CreateProcessInstanceAsync("HRProcess",
            new Dictionary<string, object> { ["department"] = "engineering" });

        // Act
        var results = await service.RechercherParVariableAsync(new List<FiltreVariable>
        {
            new FiltreVariable("department", "hr")
        });

        // Assert
        Assert.Empty(results);
    }

    // -------------------------------------------------------------------------
    // Node history retrieval
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Obtenir_AfterProcessExecution_ReturnsAccurateNodeHistoryEntry()
    {
        // Arrange: a simple single-node business process
        var repo = CreateRepository();
        var handler = CreateBusinessHandler();
        var def = new ProcessDefinition("SimpleFlow", "1.0");
        def.AddNode(new BusinessNode("ProcessOrder")
            { Name = "ProcessOrder", DisplayName = "Process Order" });

        var service = CreateService(repo, new[] { def }, handler);
        var processId = await service.CreateProcessInstanceAsync("SimpleFlow",
            new Dictionary<string, object> { ["orderId"] = "ORD-XYZ" });

        var historyId = repo.GetLastHistoryId(processId);
        Assert.NotNull(historyId);

        // Act
        var nodeHistory = await service.ObtenirNoeudAsync(historyId!.Value);

        // Assert: history entry accurately reflects the executed node
        Assert.Equal(processId, nodeHistory.ProcessId);
        Assert.Equal("ProcessOrder", nodeHistory.NodeId);
        Assert.Equal("Process Order", nodeHistory.NodeName);
        Assert.Equal(NodeType.Business, nodeHistory.NodeType);
        Assert.True(nodeHistory.Success);
        Assert.Null(nodeHistory.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Child process retrieval
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OrderProcess_WithPausedSubProcess_ObtenirEnfantsReturnsChildProcess()
    {
        // Arrange: parent process that spawns a sub-process; the sub-process
        //          pauses at an interactive node so it remains in the repository.
        var repo = CreateRepository();

        var subDef = new ProcessDefinition("TaxCalculation", "1.0");
        var subInteractive = new InteractiveNode { Name = "ReviewTax", DisplayName = "Review Tax" };
        var subDone = new BusinessNode("SubDone") { Name = "SubDone", DisplayName = "Sub Done" };
        subInteractive.NextNodeIds.Add("SubDone");
        subDef.AddNode(subInteractive);
        subDef.AddNode(subDone);

        var parentDef = new ProcessDefinition("OrderProcess", "1.0");
        var subProcessNode = new SubProcessNode(subDef)
        {
            Name = "RunTaxCalc",
            DisplayName = "Tax Calculation"
        };
        parentDef.AddNode(subProcessNode);

        var service = CreateService(repo, new[] { parentDef });

        // Act: creating the order starts the sub-process, which pauses
        var parentId = await service.CreateProcessInstanceAsync("OrderProcess",
            new Dictionary<string, object> { ["orderId"] = "ORD-007" });

        var children = await service.ObtenirEnfantsAsync(parentId);

        // Assert: one child process waiting for interaction on the sub-process's interactive step
        Assert.Single(children);
        Assert.Equal(ProcessStatus.WaitingInteraction, children[0].Status);
        Assert.Equal("TaxCalculation", children[0].DefinitionName);
    }

    // -------------------------------------------------------------------------
    // End-to-end business scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HelpDeskTicket_FullLifecycle_SubmittedFormDrivesDecisionRouting()
    {
        // Arrange: after the user submits the ticket form the engine evaluates a decision
        //          node and routes to the correct handler based on the priority variable.
        //
        // Flow: CollectTicket (interactive, pauses)
        //     → PrepareRoute  (business bridge – ContinueAsync jumps past it)
        //     → RouteByPriority (decision evaluated by ExecuteAsync after bridge)
        //     → CriticalHandler | StandardHandler
        var repo = CreateRepository();
        var executedNodes = new List<string>();
        var handler = CreateBusinessHandler((name, _) => executedNodes.Add(name));

        var def = new ProcessDefinition("HelpDesk", "1.0");

        var collectTicket = new InteractiveNode { Name = "CollectTicket", DisplayName = "Collect Ticket Info" };
        var prepareRoute = new BusinessNode("PrepareRoute") { Name = "PrepareRoute", DisplayName = "Prepare Route" };
        var routeByPriority = new DecisionNode { Name = "RouteByPriority", DisplayName = "Route by Priority" };
        routeByPriority.AddCondition("priority", "critical", OperateurFiltre.Egal, TypeDonnee.Texte, "CriticalHandler");
        routeByPriority.SetNoeudParDefaut("StandardHandler");

        var criticalNode = new BusinessNode("Critical") { Name = "CriticalHandler", DisplayName = "Critical Path" };
        var standardNode = new BusinessNode("Standard") { Name = "StandardHandler", DisplayName = "Standard Path" };

        collectTicket.NextNodeIds.Add("PrepareRoute");
        prepareRoute.NextNodeIds.Add("RouteByPriority");

        def.AddNode(collectTicket);
        def.AddNode(prepareRoute);
        def.AddNode(routeByPriority);
        def.AddNode(criticalNode);
        def.AddNode(standardNode);

        var service = CreateService(repo, new[] { def }, handler);

        // Create ticket and pause for user input
        var processId = await service.CreateProcessInstanceAsync("HelpDesk",
            new Dictionary<string, object> { ["submittedBy"] = "user-99" });
        Assert.Equal(ProcessStatus.WaitingInteraction, (await service.ObtenirAsync(processId)).Status);

        // User submits ticket details including a critical priority
        var historyId = repo.GetLastHistoryId(processId)!.Value;
        await service.TerminerEtapeAsync(historyId, new Dictionary<string, object>
        {
            ["title"] = "Production server down",
            ["priority"] = "critical",
            ["affectedSystem"] = "payments"
        });

        // Assert: ticket routed to critical handler with all variables intact
        var resolved = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, resolved.Status);
        Assert.Equal("user-99", resolved.Variables["submittedBy"]);
        Assert.Equal("Production server down", resolved.Variables["title"]);
        Assert.Equal("critical", resolved.Variables["priority"]);
        Assert.Equal("payments", resolved.Variables["affectedSystem"]);
        Assert.Contains("CriticalHandler", executedNodes);
        Assert.DoesNotContain("StandardHandler", executedNodes);
    }

    [Fact]
    public async Task PurchaseApproval_FullScenario_InteractiveSubmissionFollowedByManagerSignal()
    {
        // Arrange: employee fills a purchase request form, then the process waits
        //          for a manager approval signal before completing.
        //
        // Flow: CollectRequest (interactive, pauses)
        //     → PrepareBridge  (business bridge)
        //     → WaitManagerApproval (WaitForSignal, pauses again)
        //     → ProcessPurchase (business)
        var repo = CreateRepository();
        var handler = CreateBusinessHandler();

        var def = new ProcessDefinition("PurchaseApproval", "1.0");

        var collectRequest = new InteractiveNode
            { Name = "CollectRequest", DisplayName = "Collect Purchase Request" };
        var prepareBridge = new BusinessNode("PrepareBridge")
            { Name = "PrepareBridge", DisplayName = "Prepare Approval" };
        var waitApproval = new WaitForSignalNode("manager-approved")
            { Name = "WaitManagerApproval", DisplayName = "Await Manager Approval" };
        var processPurchase = new BusinessNode("ProcessPurchase")
            { Name = "ProcessPurchase", DisplayName = "Process Purchase" };

        collectRequest.NextNodeIds.Add("PrepareBridge");
        prepareBridge.NextNodeIds.Add("WaitManagerApproval");
        waitApproval.NextNodeIds.Add("ProcessPurchase");

        def.AddNode(collectRequest);
        def.AddNode(prepareBridge);
        def.AddNode(waitApproval);
        def.AddNode(processPurchase);

        var service = CreateService(repo, new[] { def }, handler);

        // Create process; employee fills in request form
        var processId = await service.CreateProcessInstanceAsync("PurchaseApproval",
            new Dictionary<string, object> { ["requesterId"] = "EMP-808" });

        Assert.Equal(ProcessStatus.WaitingInteraction, (await service.ObtenirAsync(processId)).Status);

        var historyId = repo.GetLastHistoryId(processId)!.Value;
        await service.TerminerEtapeAsync(historyId, new Dictionary<string, object>
        {
            ["itemDescription"] = "Development laptop",
            ["amount"] = 2500.0,
            ["businessJustification"] = "Required for remote work"
        });

        // After form submission the process should wait for manager approval signal
        var waitingForSignal = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.WaitingSignal, waitingForSignal.Status);
        Assert.Equal("EMP-808", waitingForSignal.Variables["requesterId"]);
        Assert.Equal(2500.0, waitingForSignal.Variables["amount"]);

        // The pending signal should be visible via ObtenirSignauxEnAttente
        var pendingSignals = (await service.ObtenirSignauxEnAttenteAsync(processId)).ToList();
        Assert.Contains("manager-approved", pendingSignals);

        // Manager sends the approval signal
        await service.EnvoyerSignalAsync(processId, "manager-approved");

        // Assert: process completes; all variables from both phases are preserved
        var approved = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, approved.Status);
        Assert.Equal("EMP-808", approved.Variables["requesterId"]);
        Assert.Equal("Development laptop", approved.Variables["itemDescription"]);
        Assert.Equal(2500.0, approved.Variables["amount"]);
        Assert.Equal("Required for remote work", approved.Variables["businessJustification"]);
    }
}
