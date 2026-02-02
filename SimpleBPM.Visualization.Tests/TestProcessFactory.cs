using SimpleBPM;
using SimpleBPM.Nodes;

namespace SimpleBPM.Visualization.Tests;

/// <summary>
/// Shared test fixtures for building process definitions and instances.
/// </summary>
internal static class TestProcessFactory
{
    /// <summary>
    /// Creates a simple linear process: Start → Business → Business → End.
    /// </summary>
    public static ProcessDefinition CreateLinearProcess()
    {
        var node1 = new BusinessNode("ValidateOrder") { Name = "Validate Order" };
        var node2 = new BusinessNode("ProcessOrder") { Name = "Process Order" };
        node1.NextNodeIds.Add(node2.Id);

        var definition = new ProcessDefinition("LinearProcess");
        definition.AddNode(node1);
        definition.AddNode(node2);
        return definition;
    }

    /// <summary>
    /// Creates a process with a decision node branching into two paths.
    /// </summary>
    public static ProcessDefinition CreateDecisionProcess()
    {
        var validate = new BusinessNode("ValidateOrder") { Name = "Validate Order" };
        var decide = new DecisionNode("CheckStock") { Name = "Check Stock" };
        var approve = new BusinessNode("ApproveOrder") { Name = "Approve Order" };
        var reject = new BusinessNode("RejectOrder") { Name = "Reject Order" };

        validate.NextNodeIds.Add(decide.Id);
        decide.AddRoute("in_stock", approve.Id);
        decide.AddRoute("out_of_stock", reject.Id);

        var definition = new ProcessDefinition("OrderDecision");
        definition.AddNode(validate);
        definition.AddNode(decide);
        definition.AddNode(approve);
        definition.AddNode(reject);
        return definition;
    }

    /// <summary>
    /// Creates a process with all node types represented.
    /// </summary>
    public static ProcessDefinition CreateAllNodeTypesProcess()
    {
        var business = new BusinessNode("DoWork") { Name = "Do Work" };
        var decision = new DecisionNode("Evaluate") { Name = "Evaluate" };
        var interactive = new InteractiveNode() { Name = "Manual Review" };
        var waitDate = new WaitUntilDateNode("dueDate") { Name = "Wait for Due Date" };
        var waitSignal = new WaitForSignalNode("PaymentReceived") { Name = "Wait for Payment" };

        var subDef = new ProcessDefinition("SubValidation");
        var subNode = new BusinessNode("SubCheck") { Name = "Sub Check" };
        subDef.AddNode(subNode);
        var subProcess = new SubProcessNode(subDef) { Name = "Run Validation" };

        business.NextNodeIds.Add(decision.Id);
        decision.AddRoute("pass", interactive.Id);
        decision.AddRoute("fail", waitSignal.Id);
        interactive.NextNodeIds.Add(waitDate.Id);
        waitDate.NextNodeIds.Add(subProcess.Id);

        var definition = new ProcessDefinition("AllTypes");
        definition.AddNode(business);
        definition.AddNode(decision);
        definition.AddNode(interactive);
        definition.AddNode(waitDate);
        definition.AddNode(waitSignal);
        definition.AddNode(subProcess);
        return definition;
    }

    /// <summary>
    /// Creates a process instance that has partially executed through a decision process.
    /// The first two nodes are completed and the process is waiting at an interactive node.
    /// </summary>
    public static (ProcessDefinition Definition, ProcessInstance Instance) CreatePartiallyExecutedProcess()
    {
        var validate = new BusinessNode("ValidateOrder") { Name = "Validate Order" };
        var decide = new DecisionNode("CheckStock") { Name = "Check Stock" };
        var approve = new InteractiveNode() { Name = "Manual Approval" };
        var reject = new BusinessNode("RejectOrder") { Name = "Reject Order" };
        var complete = new BusinessNode("CompleteOrder") { Name = "Complete Order" };

        validate.NextNodeIds.Add(decide.Id);
        decide.AddRoute("approved", approve.Id);
        decide.AddRoute("rejected", reject.Id);
        approve.NextNodeIds.Add(complete.Id);

        var definition = new ProcessDefinition("OrderProcess");
        definition.AddNode(validate);
        definition.AddNode(decide);
        definition.AddNode(approve);
        definition.AddNode(reject);
        definition.AddNode(complete);

        var instance = new ProcessInstance("proc-1", "agg-1")
        {
            DefinitionName = "OrderProcess",
            DefinitionVersion = "1.0",
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = approve.Id
        };

        // Simulate execution history
        instance.ExecutionHistory.Add(CreateHistory(validate.Id, validate.Name, NodeType.Business, true));
        instance.ExecutionHistory.Add(CreateHistory(decide.Id, decide.Name, NodeType.Decision, true));

        return (definition, instance);
    }

    /// <summary>
    /// Creates a completed process instance.
    /// </summary>
    public static (ProcessDefinition Definition, ProcessInstance Instance) CreateCompletedProcess()
    {
        var node1 = new BusinessNode("Step1") { Name = "Step 1" };
        var node2 = new BusinessNode("Step2") { Name = "Step 2" };
        node1.NextNodeIds.Add(node2.Id);

        var definition = new ProcessDefinition("SimpleProcess");
        definition.AddNode(node1);
        definition.AddNode(node2);

        var instance = new ProcessInstance("proc-2")
        {
            Status = ProcessStatus.Completed,
            CurrentNodeId = node2.Id,
            CompletedAt = DateTime.UtcNow
        };

        instance.ExecutionHistory.Add(CreateHistory(node1.Id, node1.Name, NodeType.Business, true));
        instance.ExecutionHistory.Add(CreateHistory(node2.Id, node2.Name, NodeType.Business, true));

        return (definition, instance);
    }

    /// <summary>
    /// Creates a process instance with a failed node.
    /// </summary>
    public static (ProcessDefinition Definition, ProcessInstance Instance) CreateFailedProcess()
    {
        var node1 = new BusinessNode("Step1") { Name = "Step 1" };
        var node2 = new BusinessNode("Step2") { Name = "Step 2" };
        node1.NextNodeIds.Add(node2.Id);

        var definition = new ProcessDefinition("FailingProcess");
        definition.AddNode(node1);
        definition.AddNode(node2);

        var instance = new ProcessInstance("proc-3")
        {
            Status = ProcessStatus.Failed,
            CurrentNodeId = node2.Id,
            ErrorMessage = "Something went wrong"
        };

        instance.ExecutionHistory.Add(CreateHistory(node1.Id, node1.Name, NodeType.Business, true));
        instance.ExecutionHistory.Add(CreateHistory(node2.Id, node2.Name, NodeType.Business, false, "Something went wrong"));

        return (definition, instance);
    }

    private static NodeExecutionHistory CreateHistory(string nodeId, string nodeName, NodeType type, bool success, string? error = null)
    {
        var h = new NodeExecutionHistory(nodeId, nodeName, type);
        h.Complete(success, error);
        return h;
    }
}
