using SimpleBPM.Definition;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests;

public class ProcessBuilderTests
{
    [Fact]
    public void Build_LinearChain_ConnectsNodesSequentially()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("A")
            .Business("B")
            .Business("C")
            .Build();

        Assert.Equal(3, definition.Nodes.Count);

        var nodeA = definition.GetNodeByName("A")!;
        var nodeB = definition.GetNodeByName("B")!;
        var nodeC = definition.GetNodeByName("C")!;

        Assert.Single(nodeA.NextNodeIds, nodeB.Id);
        Assert.Single(nodeB.NextNodeIds, nodeC.Id);
        Assert.Empty(nodeC.NextNodeIds);
    }

    [Fact]
    public void Build_FirstNodeBecomesStart()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("First")
            .Business("Second")
            .Build();

        var firstNode = definition.GetNodeByName("First")!;
        Assert.Equal(firstNode.Id, definition.StartNodeId);
    }

    [Fact]
    public void Build_SetsNameAndVersion()
    {
        var definition = ProcessBuilder.Create("MyProcess", "3.0")
            .Business("Step")
            .Build();

        Assert.Equal("MyProcess", definition.Name);
        Assert.Equal("3.0", definition.Version);
    }

    [Fact]
    public void Build_DefaultVersion_IsOnePointZero()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Step")
            .Build();

        Assert.Equal("1.0", definition.Version);
    }

    [Fact]
    public void Build_DecisionNode_RoutesResolved()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("Start")
            .Decision("Check", routes => routes
                .When("yes", "Approve")
                .When("no", "Reject"))
            .Business("Approve")
            .Business("Reject")
            .Build();

        var decision = definition.GetNodeByName("Check") as DecisionNode;
        Assert.NotNull(decision);

        var approveNode = definition.GetNodeByName("Approve")!;
        var rejectNode = definition.GetNodeByName("Reject")!;

        Assert.Equal(approveNode.Id, decision.ConditionToNodeId["yes"]);
        Assert.Equal(rejectNode.Id, decision.ConditionToNodeId["no"]);
    }

    [Fact]
    public void Build_Then_OverridesAutoChaining()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("A")
            .Business("B")
                .Then("D")
            .Business("C")
            .Business("D")
            .Build();

        var nodeB = definition.GetNodeByName("B")!;
        var nodeC = definition.GetNodeByName("C")!;
        var nodeD = definition.GetNodeByName("D")!;

        Assert.Contains(nodeC.Id, nodeB.NextNodeIds);
        Assert.Contains(nodeD.Id, nodeB.NextNodeIds);
    }

    [Fact]
    public void Build_Then_MissingTarget_Throws()
    {
        var builder = ProcessBuilder.Create("Test")
            .Business("A")
                .Then("NonExistent");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_InteractiveNode_Created()
    {
        var definition = ProcessBuilder.Create("Test")
            .Interactive("Review", "Manual Review")
            .Build();

        var node = definition.GetNodeByName("Manual Review");
        Assert.NotNull(node);
        Assert.Equal(NodeType.Interactive, node.Type);
    }

    [Fact]
    public void Build_WaitForSignalNode_Created()
    {
        var definition = ProcessBuilder.Create("Test")
            .WaitForSignal("PaymentDone", "Wait for payment")
            .Build();

        var node = definition.GetNodeByName("Wait for payment") as WaitForSignalNode;
        Assert.NotNull(node);
        Assert.Equal("PaymentDone", node.SignalName);
    }

    [Fact]
    public void Build_SubProcess_Inline_Created()
    {
        var definition = ProcessBuilder.Create("Main")
            .Business("Start")
            .SubProcess("Validation", sub => sub
                .Business("Validate")
                .Interactive("Approve"))
            .Business("End")
            .Build();

        var subNode = definition.GetNodeByName("Validation") as SubProcessNode;
        Assert.NotNull(subNode);
        Assert.Equal(2, subNode.SubProcessDefinition.Nodes.Count);
    }

    [Fact]
    public void Build_SubProcess_WithMappings()
    {
        var subDef = ProcessBuilder.Create("Sub")
            .Business("SubStep")
            .Build();

        var definition = ProcessBuilder.Create("Main")
            .SubProcess("MySub", subDef,
                inputMapping: new() { ["ParentVar"] = "SubVar" },
                outputMapping: new() { ["SubResult"] = "ParentResult" })
            .Build();

        var subNode = definition.GetNodeByName("MySub") as SubProcessNode;
        Assert.NotNull(subNode);
        Assert.Equal("SubVar", subNode.InputMapping["ParentVar"]);
        Assert.Equal("ParentResult", subNode.OutputMapping["SubResult"]);
    }

    [Fact]
    public void Build_StartWith_OverridesDefaultStart()
    {
        var definition = ProcessBuilder.Create("Test")
            .Business("A")
            .Business("B")
            .StartWith("B")
            .Build();

        var nodeB = definition.GetNodeByName("B")!;
        Assert.Equal(nodeB.Id, definition.StartNodeId);
    }

    [Fact]
    public void Build_AllNodeTypes_HaveCorrectTypes()
    {
        var subDef = ProcessBuilder.Create("Sub").Business("S").Build();

        var definition = ProcessBuilder.Create("Test")
            .Business("B1")
            .Decision("D1", routes => routes.When("x", "B1"))
            .Interactive("I1")
            .WaitForSignal("WS1")
            .WaitUntilDate("WD1", "dateKey")
            .SubProcess("SP1", subDef)
            .Build();

        Assert.Equal(NodeType.Business, definition.GetNodeByName("B1")!.Type);
        Assert.Equal(NodeType.Decision, definition.GetNodeByName("D1")!.Type);
        Assert.Equal(NodeType.Interactive, definition.GetNodeByName("I1")!.Type);
        Assert.Equal(NodeType.WaitForSignal, definition.GetNodeByName("WS1")!.Type);
        Assert.Equal(NodeType.WaitUntilDate, definition.GetNodeByName("WD1")!.Type);
        Assert.Equal(NodeType.SubProcess, definition.GetNodeByName("SP1")!.Type);
    }
}
