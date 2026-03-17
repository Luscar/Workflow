using SimpleBPM;
using SimpleBPM.Definition;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests;

public class ProcessBuilderTests
{
    [Fact]
    public void Build_SimpleLinearProcess()
    {
        var def = ProcessBuilder.Create("OrderProcess", "1.0")
            .Business("CreateOrder", "Créer commande")
            .Business("ValidateOrder", "Valider commande")
            .Business("ShipOrder", "Expédier commande")
            .Build();

        Assert.Equal("OrderProcess", def.Name);
        Assert.Equal("1.0", def.Version);
        Assert.Equal(3, def.Nodes.Count);
        Assert.Equal("CreateOrder", def.StartNodeId);
    }

    [Fact]
    public void Build_AutoConnectsSequentialNodes()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Business("Step2")
            .Business("Step3")
            .Build();

        var step1 = def.GetNode("Step1");
        var step2 = def.GetNode("Step2");

        Assert.Contains("Step2", step1!.NextNodeIds);
        Assert.Contains("Step3", step2!.NextNodeIds);
    }

    [Fact]
    public void Build_WithInteractiveNode()
    {
        var def = ProcessBuilder.Create("ApprovalProcess")
            .Business("Prepare")
            .Interactive("Review", "Revue manuelle")
            .Business("Complete")
            .Build();

        Assert.Equal(3, def.Nodes.Count);
        var reviewNode = def.GetNode("Review");
        Assert.NotNull(reviewNode);
        Assert.Equal(NodeType.Interactive, reviewNode.Type);
    }

    [Fact]
    public void Build_WithDecisionNode()
    {
        var def = ProcessBuilder.Create("DecisionProcess")
            .Business("Prepare")
            .Decision("CheckStatus", routes =>
            {
                routes.When("approved", "Approve");
                routes.When("rejected", "Reject");
            })
            .Business("Approve", "Approuver")
            .Business("Reject", "Rejeter")
            .Build();

        var decisionNode = def.GetNode("CheckStatus") as DecisionNode;
        Assert.NotNull(decisionNode);
        Assert.Equal("Approve", decisionNode.ConditionToNodeId["approved"]);
        Assert.Equal("Reject", decisionNode.ConditionToNodeId["rejected"]);
    }

    [Fact]
    public void Build_WithWaitForSignal()
    {
        var def = ProcessBuilder.Create("SignalProcess")
            .Business("Init")
            .WaitForSignal("approval-signal", "Attente approbation")
            .Business("Finalize")
            .Build();

        var signalNode = def.GetNode("approval-signal") as WaitForSignalNode;
        Assert.NotNull(signalNode);
        Assert.Equal("approval-signal", signalNode.SignalName);
    }

    [Fact]
    public void Build_WithWaitUntilDate()
    {
        var def = ProcessBuilder.Create("DateProcess")
            .Business("Init")
            .WaitUntilDate("WaitDue", "DueDate", "Attente échéance")
            .Business("Finalize")
            .Build();

        var waitNode = def.GetNode("WaitDue") as WaitUntilDateNode;
        Assert.NotNull(waitNode);
        Assert.Equal("DueDate", waitNode.DateKey);
    }

    [Fact]
    public void Build_WithParameters()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("Step1")
            .WithParameter("key1", "value1")
            .WithParameter("key2", 42)
            .Build();

        var node = def.GetNode("Step1");
        Assert.Equal("value1", node!.Parameters["key1"]);
        Assert.Equal(42, node.Parameters["key2"]);
    }

    [Fact]
    public void Build_WithParametersDictionary()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("Step1")
            .WithParameters(new Dictionary<string, object>
            {
                { "param1", "a" },
                { "param2", "b" }
            })
            .Build();

        var node = def.GetNode("Step1");
        Assert.Equal(2, node!.Parameters.Count);
    }

    [Fact]
    public void Build_WithThen_ConnectsNodes()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("Start")
            .Business("End")
            .Then("Start")
            .Build();

        var endNode = def.GetNode("End");
        Assert.Contains("Start", endNode!.NextNodeIds);
    }

    [Fact]
    public void Build_WithStartWith_SetsStartNode()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Business("Step2")
            .StartWith("Step2")
            .Build();

        Assert.Equal("Step2", def.StartNodeId);
    }

    [Fact]
    public void Build_WithSubProcess()
    {
        var subDef = ProcessBuilder.Create("SubProcess")
            .Business("SubStep1")
            .Business("SubStep2")
            .Build();

        var def = ProcessBuilder.Create("MainProcess")
            .Business("Init")
            .SubProcess("RunSub", subDef,
                inputMapping: new() { { "parentVar", "subVar" } },
                outputMapping: new() { { "subResult", "parentResult" } })
            .Business("Finalize")
            .Build();

        var subNode = def.GetNode("RunSub") as SubProcessNode;
        Assert.NotNull(subNode);
        Assert.Equal("subVar", subNode.InputMapping["parentVar"]);
        Assert.Equal("parentResult", subNode.OutputMapping["subResult"]);
    }

    [Fact]
    public void Build_WithInlineSubProcess()
    {
        var def = ProcessBuilder.Create("MainProcess")
            .Business("Init")
            .SubProcess("RunSub", sub =>
            {
                sub.Business("SubStep1");
                sub.Business("SubStep2");
            })
            .Business("Finalize")
            .Build();

        var subNode = def.GetNode("RunSub") as SubProcessNode;
        Assert.NotNull(subNode);
        Assert.Equal(2, subNode.SubProcessDefinition.Nodes.Count);
    }

    [Fact]
    public void Build_WithWaitUntilDate_StaticDate()
    {
        var targetDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var def = ProcessBuilder.Create("DateProcess")
            .Business("Init")
            .WaitUntilDate("WaitDeadline", targetDate, "Attente deadline")
            .Business("Finalize")
            .Build();

        var waitNode = def.GetNode("WaitDeadline") as WaitUntilDateNode;
        Assert.NotNull(waitNode);
        Assert.Equal(targetDate, waitNode.TargetDate);
        Assert.Equal("Attente deadline", waitNode.DisplayName);
    }

    [Fact]
    public void Build_WithWaitUntilDate_DateProvider()
    {
        Func<ProcessInstance, DateTime> provider = _ => DateTime.UtcNow.AddDays(7);

        var def = ProcessBuilder.Create("DateProcess")
            .Business("Init")
            .WaitUntilDate("WaitDynamic", provider)
            .Business("Finalize")
            .Build();

        var waitNode = def.GetNode("WaitDynamic") as WaitUntilDateNode;
        Assert.NotNull(waitNode);
        Assert.NotNull(waitNode.DateProvider);
        Assert.Null(waitNode.TargetDate);
        Assert.Null(waitNode.DateKey);
    }

    [Fact]
    public void Build_WithOnEnterCommand_SetsProperty()
    {
        var def = ProcessBuilder.Create("NotifyProcess")
            .Business("Init")
            .Interactive("Review")
                .WithOnEnterCommand("NotifyReviewer")
            .Business("Complete")
            .Build();

        var reviewNode = def.GetNode("Review");
        Assert.NotNull(reviewNode);
        Assert.Equal("NotifyReviewer", reviewNode.OnEnterCommandName);
    }

    [Fact]
    public void Build_WithOnEnterCommand_OnWaitForSignal()
    {
        var def = ProcessBuilder.Create("SignalProcess")
            .Business("Init")
            .WaitForSignal("approval")
                .WithOnEnterCommand("SendApprovalRequest")
            .Business("Done")
            .Build();

        var signalNode = def.GetNode("approval");
        Assert.Equal("SendApprovalRequest", signalNode!.OnEnterCommandName);
    }

    [Fact]
    public void Build_WithOnEnterCommandParameter_SetsParameters()
    {
        var def = ProcessBuilder.Create("NotifyProcess")
            .Business("Init")
            .Interactive("Review")
                .WithOnEnterCommand("NotifyReviewer")
                .WithOnEnterCommandParameter("priority", "high")
                .WithOnEnterCommandParameter("dueInDays", 3)
            .Business("Complete")
            .Build();

        var node = def.GetNode("Review");
        Assert.Equal("high", node!.OnEnterCommandParameters["priority"]);
        Assert.Equal(3, node.OnEnterCommandParameters["dueInDays"]);
    }

    [Fact]
    public void WithOnEnterCommandParameter_NoCurrentNode_Throws()
    {
        var builder = ProcessBuilder.Create("Test");
        Assert.Throws<InvalidOperationException>(() => builder.WithOnEnterCommandParameter("key", "val"));
    }

    [Fact]
    public void WithOnEnterCommand_NoCurrentNode_Throws()
    {
        var builder = ProcessBuilder.Create("Test");
        Assert.Throws<InvalidOperationException>(() => builder.WithOnEnterCommand("Cmd"));
    }

    [Fact]
    public void Build_InvalidReference_Throws()
    {
        var builder = ProcessBuilder.Create("Test")
            .Business("Step1")
            .Then("NonExistent");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void WithParameter_NoCurrentNode_Throws()
    {
        var builder = ProcessBuilder.Create("Test");
        Assert.Throws<InvalidOperationException>(() => builder.WithParameter("key", "val"));
    }

    [Fact]
    public void Then_NoCurrentNode_Throws()
    {
        var builder = ProcessBuilder.Create("Test");
        Assert.Throws<InvalidOperationException>(() => builder.Then("node"));
    }

    [Fact]
    public void Build_WithEndNode_IsTerminal()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("DoWork")
            .End("Done", "Work Done")
            .Build();

        var endNode = def.GetNode("Done");
        Assert.NotNull(endNode);
        Assert.Equal(NodeType.End, endNode.Type);
        Assert.Equal("Work Done", endNode.DisplayName);
        Assert.Empty(endNode.NextNodeIds);
    }

    [Fact]
    public void Build_EndNode_BreaksAutoLinkChain()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("Rejected")
            .End("TerminateRejected")
            .Business("Approved")
            .Build();

        var rejected = def.GetNode("Rejected");
        var approved = def.GetNode("Approved");

        // Rejected links to TerminateRejected, not to Approved
        Assert.Contains("TerminateRejected", rejected!.NextNodeIds);
        Assert.DoesNotContain("Approved", rejected.NextNodeIds);

        // Approved has no next (declared after the broken chain)
        Assert.Empty(approved!.NextNodeIds);
    }

    [Fact]
    public void Build_ThenPreventsAutoLink()
    {
        var def = ProcessBuilder.Create("Test")
            .Business("A")
                .Then("C")
            .Business("B")  // A already has an explicit next, so B should NOT be auto-linked from A
            .Business("C")
            .Build();

        var nodeA = def.GetNode("A");
        Assert.Equal(new[] { "C" }, nodeA!.NextNodeIds);
    }

    [Fact]
    public void Build_BranchedProcess_CorrectLinks()
    {
        var def = ProcessBuilder.Create("LoanTest")
            .Business("Decide")
                .Then("Approved")
            .Business("Rejected")
                .End("EndRejected")
            .Business("Approved")
                .End("EndApproved")
            .Build();

        var rejected = def.GetNode("Rejected");
        var approved = def.GetNode("Approved");

        Assert.Equal(new[] { "EndRejected" }, rejected!.NextNodeIds);
        Assert.Equal(new[] { "EndApproved" }, approved!.NextNodeIds);
        Assert.Equal(NodeType.End, def.GetNode("EndRejected")!.Type);
        Assert.Equal(NodeType.End, def.GetNode("EndApproved")!.Type);
    }

    [Fact]
    public void Business_NodeName_Distinct_From_CommandName_AllowsCommandReuse()
    {
        // Même commande "Validate" utilisée sur deux nœuds différents
        var def = ProcessBuilder.Create("ReusedCommandProcess")
            .Business("ValidateStep1", "Validate", "Validation étape 1")
            .Business("ValidateStep2", "Validate", "Validation étape 2")
            .Business("Complete")
            .Build();

        Assert.Equal(3, def.Nodes.Count);

        var node1 = def.GetNode("ValidateStep1") as BusinessNode;
        var node2 = def.GetNode("ValidateStep2") as BusinessNode;
        var complete = def.GetNode("Complete");

        Assert.NotNull(node1);
        Assert.NotNull(node2);
        Assert.NotNull(complete);

        // Les deux nœuds pointent vers la même commande
        Assert.Equal("Validate", node1!.CommandName);
        Assert.Equal("Validate", node2!.CommandName);

        // Le routage reste correct : Step1 → Step2 → Complete
        Assert.Equal(new[] { "ValidateStep2" }, node1.NextNodeIds);
        Assert.Equal(new[] { "Complete" }, node2.NextNodeIds);
    }
}
