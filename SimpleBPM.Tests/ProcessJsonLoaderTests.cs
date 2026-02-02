using SimpleBPM.Definition;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests;

public class ProcessJsonLoaderTests
{
    [Fact]
    public void FromJson_SimpleProcess_ParsesCorrectly()
    {
        var json = """
        {
            "name": "TestProcess",
            "version": "2.0",
            "startNode": "Step1",
            "nodes": [
                { "name": "Step1", "type": "Business", "command": "DoStep1", "next": ["Step2"] },
                { "name": "Step2", "type": "Business", "command": "DoStep2" }
            ]
        }
        """;

        var definition = ProcessJsonLoader.FromJson(json);

        Assert.Equal("TestProcess", definition.Name);
        Assert.Equal("2.0", definition.Version);
        Assert.Equal(2, definition.Nodes.Count);

        var step1 = definition.GetNodeByName("Step1") as BusinessNode;
        Assert.NotNull(step1);
        Assert.Equal("DoStep1", step1.CommandName);
        Assert.Equal(definition.StartNodeId, step1.Id);
    }

    [Fact]
    public void FromJson_DecisionNode_RoutesResolved()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                {
                    "name": "Check",
                    "type": "Decision",
                    "query": "EvalCheck",
                    "routes": { "yes": "Approve", "no": "Reject" },
                    "next": ["Approve", "Reject"]
                },
                { "name": "Approve", "type": "Business", "command": "Approve" },
                { "name": "Reject", "type": "Business", "command": "Reject" }
            ]
        }
        """;

        var definition = ProcessJsonLoader.FromJson(json);
        var decision = definition.GetNodeByName("Check") as DecisionNode;

        Assert.NotNull(decision);
        Assert.Equal("EvalCheck", decision.QueryName);

        var approveNode = definition.GetNodeByName("Approve")!;
        var rejectNode = definition.GetNodeByName("Reject")!;
        Assert.Equal(approveNode.Id, decision.ConditionToNodeId["yes"]);
        Assert.Equal(rejectNode.Id, decision.ConditionToNodeId["no"]);
    }

    [Fact]
    public void FromJson_AllNodeTypes_Parsed()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                { "name": "B1", "type": "Business", "command": "Cmd" },
                { "name": "I1", "type": "Interactive" },
                { "name": "WS1", "type": "WaitForSignal", "signal": "Sig1" },
                { "name": "WD1", "type": "WaitUntilDate", "dateKey": "dueDate" }
            ]
        }
        """;

        var definition = ProcessJsonLoader.FromJson(json);

        Assert.Equal(NodeType.Business, definition.GetNodeByName("B1")!.Type);
        Assert.Equal(NodeType.Interactive, definition.GetNodeByName("I1")!.Type);

        var signalNode = definition.GetNodeByName("WS1") as WaitForSignalNode;
        Assert.NotNull(signalNode);
        Assert.Equal("Sig1", signalNode.SignalName);

        var dateNode = definition.GetNodeByName("WD1") as WaitUntilDateNode;
        Assert.NotNull(dateNode);
        Assert.Equal("dueDate", dateNode.DateKey);
    }

    [Fact]
    public void FromJson_SubProcess_Nested()
    {
        var json = """
        {
            "name": "Main",
            "nodes": [
                { "name": "Start", "type": "Business", "command": "Start", "next": ["Sub"] },
                {
                    "name": "Sub",
                    "type": "SubProcess",
                    "inputMapping": { "ParentVar": "SubVar" },
                    "outputMapping": { "SubResult": "ParentResult" },
                    "subProcess": {
                        "name": "SubProcess",
                        "nodes": [
                            { "name": "SubStep", "type": "Business", "command": "SubCmd" }
                        ]
                    }
                }
            ]
        }
        """;

        var definition = ProcessJsonLoader.FromJson(json);
        var subNode = definition.GetNodeByName("Sub") as SubProcessNode;

        Assert.NotNull(subNode);
        Assert.Equal("SubVar", subNode.InputMapping["ParentVar"]);
        Assert.Equal("ParentResult", subNode.OutputMapping["SubResult"]);
        Assert.Single(subNode.SubProcessDefinition.Nodes);
    }

    [Fact]
    public void FromJson_DefaultStartNode_IsFirstNode()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                { "name": "First", "type": "Business", "command": "Cmd1" },
                { "name": "Second", "type": "Business", "command": "Cmd2" }
            ]
        }
        """;

        var definition = ProcessJsonLoader.FromJson(json);
        var firstNode = definition.GetNodeByName("First")!;
        Assert.Equal(firstNode.Id, definition.StartNodeId);
    }

    [Fact]
    public void ToJson_RoundTrip_PreservesStructure()
    {
        var original = ProcessBuilder.Create("RoundTrip", "1.5")
            .Business("Step1", "First")
            .Business("Step2", "Second")
            .Build();

        var json = ProcessJsonLoader.ToJson(original);
        var restored = ProcessJsonLoader.FromJson(json);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.Nodes.Count, restored.Nodes.Count);

        var restoredStep1 = restored.GetNodeByName("First") as BusinessNode;
        Assert.NotNull(restoredStep1);
        Assert.Equal("Step1", restoredStep1.CommandName);
    }

    [Fact]
    public void ToJson_DecisionNode_RoundTrip()
    {
        var original = ProcessBuilder.Create("Test")
            .Decision("Check", "Decision", routes => routes
                .When("a", "NodeA")
                .When("b", "NodeB"))
            .Business("NodeA")
            .Business("NodeB")
            .Build();

        var json = ProcessJsonLoader.ToJson(original);
        var restored = ProcessJsonLoader.FromJson(json);

        var decision = restored.GetNodeByName("Decision") as DecisionNode;
        Assert.NotNull(decision);
        Assert.Equal(2, decision.ConditionToNodeId.Count);
    }

    [Fact]
    public void FromJson_InvalidJson_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => ProcessJsonLoader.FromJson("not valid json"));
    }

    [Fact]
    public void FromJson_MissingNodeReference_Throws()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                { "name": "A", "type": "Business", "command": "A", "next": ["NonExistent"] }
            ]
        }
        """;

        Assert.Throws<InvalidOperationException>(
            () => ProcessJsonLoader.FromJson(json));
    }
}
