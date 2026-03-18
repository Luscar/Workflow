using SimpleBPM;
using SimpleBPM.Definition;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests;

public class ProcessJsonLoaderTests
{
    [Fact]
    public void FromJson_SimpleProcess()
    {
        var json = """
        {
            "name": "TestProcess",
            "version": "1.0",
            "startNode": "Step1",
            "nodes": [
                {
                    "name": "Step1",
                    "type": "Business",
                    "displayName": "Étape 1",
                    "command": "CreateOrder",
                    "next": ["Step2"]
                },
                {
                    "name": "Step2",
                    "type": "Business",
                    "displayName": "Étape 2",
                    "command": "ValidateOrder"
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);

        Assert.Equal("TestProcess", def.Name);
        Assert.Equal("1.0", def.Version);
        Assert.Equal("Step1", def.StartNodeId);
        Assert.Equal(2, def.Nodes.Count);

        var step1 = def.GetNode("Step1") as BusinessNode;
        Assert.NotNull(step1);
        Assert.Equal("CreateOrder", step1.CommandName);
        Assert.Contains("Step2", step1.NextNodeIds);
    }

    [Fact]
    public void FromJson_WithDecisionNode()
    {
        var json = """
        {
            "name": "DecisionProcess",
            "version": "1.0",
            "nodes": [
                {
                    "name": "Check",
                    "type": "Decision",
                    "query": "CheckEligibility",
                    "routes": {
                        "yes": "Approve",
                        "no": "Reject"
                    }
                },
                {
                    "name": "Approve",
                    "type": "Business",
                    "command": "ApproveCmd"
                },
                {
                    "name": "Reject",
                    "type": "Business",
                    "command": "RejectCmd"
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var decision = def.GetNode("Check") as DecisionNode;

        Assert.NotNull(decision);
        Assert.Equal("CheckEligibility", decision.QueryName);
        Assert.Equal("Approve", decision.ConditionToNodeId["yes"]);
        Assert.Equal("Reject", decision.ConditionToNodeId["no"]);
    }

    [Fact]
    public void FromJson_WithInteractiveNode()
    {
        var json = """
        {
            "name": "InteractiveProcess",
            "version": "1.0",
            "nodes": [
                {
                    "name": "Review",
                    "type": "Interactive",
                    "displayName": "Revue manuelle",
                    "next": ["Complete"]
                },
                {
                    "name": "Complete",
                    "type": "Business",
                    "command": "CompleteReview"
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var review = def.GetNode("Review");

        Assert.NotNull(review);
        Assert.Equal(NodeType.Interactive, review.Type);
        Assert.Equal("Revue manuelle", review.DisplayName);
    }

    [Fact]
    public void FromJson_WithWaitForSignalNode()
    {
        var json = """
        {
            "name": "SignalProcess",
            "version": "1.0",
            "nodes": [
                {
                    "name": "WaitApproval",
                    "type": "WaitForSignal",
                    "signal": "manager-approval"
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var node = def.GetNode("WaitApproval") as WaitForSignalNode;

        Assert.NotNull(node);
        Assert.Equal("manager-approval", node.SignalName);
    }

    [Fact]
    public void FromJson_WithWaitUntilDateNode()
    {
        var json = """
        {
            "name": "DateProcess",
            "version": "1.0",
            "nodes": [
                {
                    "name": "WaitDue",
                    "type": "WaitUntilDate",
                    "dateKey": "DueDate"
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var node = def.GetNode("WaitDue") as WaitUntilDateNode;

        Assert.NotNull(node);
        Assert.Equal("DueDate", node.DateKey);
    }

    [Fact]
    public void FromJson_WithParameters()
    {
        var json = """
        {
            "name": "ParamProcess",
            "version": "1.0",
            "nodes": [
                {
                    "name": "Step1",
                    "type": "Business",
                    "command": "DoSomething",
                    "parameters": {
                        "key1": "value1",
                        "key2": 42
                    }
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var node = def.GetNode("Step1");

        Assert.NotNull(node);
        Assert.Equal(2, node.Parameters.Count);
    }

    [Fact]
    public void FromJson_WithSubProcess()
    {
        var json = """
        {
            "name": "MainProcess",
            "version": "1.0",
            "nodes": [
                {
                    "name": "RunSub",
                    "type": "SubProcess",
                    "subProcess": {
                        "name": "SubProcess",
                        "version": "1.0",
                        "nodes": [
                            {
                                "name": "SubStep1",
                                "type": "Business",
                                "command": "SubCmd1"
                            }
                        ]
                    },
                    "inputMapping": { "parentVar": "subVar" },
                    "outputMapping": { "subResult": "parentResult" },
                    "inheritAggregateId": true
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var subNode = def.GetNode("RunSub") as SubProcessNode;

        Assert.NotNull(subNode);
        Assert.Equal("SubProcess", subNode.SubProcessDefinition.Name);
        Assert.Equal("subVar", subNode.InputMapping["parentVar"]);
        Assert.Equal("parentResult", subNode.OutputMapping["subResult"]);
        Assert.True(subNode.InheritAggregateId);
    }

    [Fact]
    public void ToJson_AndBackFromJson_RoundTrip()
    {
        var original = ProcessBuilder.Create("RoundTrip", "2.0")
            .Business("Step1", "Étape 1")
            .Business("Step2", "Étape 2")
            .Build();

        var json = ProcessJsonLoader.ToJson(original);
        var restored = ProcessJsonLoader.FromJson(json);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.Nodes.Count, restored.Nodes.Count);
        Assert.Equal(original.StartNodeId, restored.StartNodeId);
    }

    [Fact]
    public void FromJson_InvalidNodeReference_Throws()
    {
        var json = """
        {
            "name": "Invalid",
            "version": "1.0",
            "nodes": [
                {
                    "name": "Step1",
                    "type": "Business",
                    "command": "Cmd",
                    "next": ["NonExistent"]
                }
            ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ProcessJsonLoader.FromJson(json));
    }

    [Fact]
    public void FromJson_DefaultStartNode_IsFirstNode()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                {
                    "name": "FirstNode",
                    "type": "Business",
                    "command": "Cmd"
                }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        Assert.Equal("FirstNode", def.StartNodeId);
    }

    [Fact]
    public void FromJson_InvalidDecisionRoute_Throws()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                {
                    "name": "Decide",
                    "type": "Decision",
                    "query": "Q",
                    "routes": { "yes": "NonExistent" }
                }
            ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ProcessJsonLoader.FromJson(json));
    }

    [Fact]
    public void FromJson_WithDecisionConditions_LoadsCorrectly()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                {
                    "name": "Decide",
                    "type": "Decision",
                    "conditions": [
                        { "nomVariable": "statut", "valeur": "approuve", "operateur": "Egal", "typeDonnee": "Texte", "noeudCible": "Approved" },
                        { "nomVariable": "statut", "valeur": "refuse", "operateur": "Egal", "typeDonnee": "Texte", "noeudCible": "Rejected" }
                    ],
                    "defaultNode": "Fallback"
                },
                { "name": "Approved", "type": "End" },
                { "name": "Rejected", "type": "End" },
                { "name": "Fallback", "type": "End" }
            ]
        }
        """;

        var def = ProcessJsonLoader.FromJson(json);
        var decisionNode = def.GetNode("Decide") as DecisionNode;

        Assert.NotNull(decisionNode);
        Assert.Null(decisionNode.QueryName);
        Assert.Equal(2, decisionNode.Conditions.Count);
        Assert.Equal("statut", decisionNode.Conditions[0].NomVariable);
        Assert.Equal("Approved", decisionNode.Conditions[0].NoeudCible);
        Assert.Equal("Fallback", decisionNode.NoeudParDefaut);
    }

    [Fact]
    public void ToJson_WithDecisionConditions_RoundTrip()
    {
        var original = ProcessBuilder.Create("Test")
            .Decision("CheckStatut", conditions =>
            {
                conditions.WhenVariable("statut", "approuve", OperateurFiltre.Egal, TypeDonnee.Texte, "Approved");
                conditions.Default("Rejected");
            })
            .End("Approved")
            .Break()
            .End("Rejected")
            .Build();

        var json = ProcessJsonLoader.ToJson(original);
        var restored = ProcessJsonLoader.FromJson(json);

        var decisionNode = restored.GetNode("CheckStatut") as DecisionNode;
        Assert.NotNull(decisionNode);
        Assert.Null(decisionNode.QueryName);
        Assert.Single(decisionNode.Conditions);
        Assert.Equal("statut", decisionNode.Conditions[0].NomVariable);
        Assert.Equal("Approved", decisionNode.Conditions[0].NoeudCible);
        Assert.Equal("Rejected", decisionNode.NoeudParDefaut);
    }

    [Fact]
    public void FromJson_InvalidDecisionConditionNode_Throws()
    {
        var json = """
        {
            "name": "Test",
            "nodes": [
                {
                    "name": "Decide",
                    "type": "Decision",
                    "conditions": [
                        { "nomVariable": "x", "valeur": "y", "operateur": "Egal", "typeDonnee": "Texte", "noeudCible": "NonExistent" }
                    ]
                }
            ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ProcessJsonLoader.FromJson(json));
    }
}
