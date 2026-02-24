using SimpleBPM;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests;

public class NodeDefinitionTests
{
    [Fact]
    public void NodeDefinition_Constructor_SetsNodeType()
    {
        var node = new NodeDefinition(NodeType.Business);

        Assert.Equal(NodeType.Business, node.Type);
    }

    [Theory]
    [InlineData(NodeType.Business)]
    [InlineData(NodeType.Decision)]
    [InlineData(NodeType.Interactive)]
    [InlineData(NodeType.WaitUntilDate)]
    [InlineData(NodeType.WaitForSignal)]
    [InlineData(NodeType.SubProcess)]
    public void NodeDefinition_Constructor_SupportsAllNodeTypes(NodeType type)
    {
        var node = new NodeDefinition(type);
        Assert.Equal(type, node.Type);
    }

    [Fact]
    public void NodeDefinition_NextNodeIds_InitializedEmpty()
    {
        var node = new NodeDefinition(NodeType.Business);
        Assert.NotNull(node.NextNodeIds);
        Assert.Empty(node.NextNodeIds);
    }

    [Fact]
    public void NodeDefinition_Parameters_InitializedEmpty()
    {
        var node = new NodeDefinition(NodeType.Business);
        Assert.NotNull(node.Parameters);
        Assert.Empty(node.Parameters);
    }

    [Fact]
    public void NodeDefinition_CanSetProperties()
    {
        var node = new NodeDefinition(NodeType.Interactive)
        {
            Name = "step1",
            DisplayName = "Étape 1"
        };

        Assert.Equal("step1", node.Name);
        Assert.Equal("Étape 1", node.DisplayName);
    }

    [Fact]
    public void BusinessNode_SetsCommandName()
    {
        var node = new BusinessNode("CreateOrder");

        Assert.Equal("CreateOrder", node.CommandName);
        Assert.Equal(NodeType.Business, node.Type);
    }

    [Fact]
    public void InteractiveNode_HasCorrectType()
    {
        var node = new InteractiveNode();
        Assert.Equal(NodeType.Interactive, node.Type);
    }

    [Fact]
    public void WaitForSignalNode_SetsSignalName()
    {
        var node = new WaitForSignalNode("approval-signal");

        Assert.Equal("approval-signal", node.SignalName);
        Assert.Equal(NodeType.WaitForSignal, node.Type);
    }

    [Fact]
    public void WaitUntilDateNode_WithTargetDate()
    {
        var targetDate = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var node = new WaitUntilDateNode(targetDate);

        Assert.Equal(targetDate, node.TargetDate);
        Assert.Equal(NodeType.WaitUntilDate, node.Type);
    }

    [Fact]
    public void WaitUntilDateNode_WithDateKey()
    {
        var node = new WaitUntilDateNode("DueDate");

        Assert.Equal("DueDate", node.DateKey);
        Assert.Equal(NodeType.WaitUntilDate, node.Type);
    }

    [Fact]
    public void WaitUntilDateNode_WithDateProvider()
    {
        Func<ProcessInstance, DateTime> provider = instance => DateTime.UtcNow.AddDays(1);
        var node = new WaitUntilDateNode(provider);

        Assert.NotNull(node.DateProvider);
        Assert.Equal(NodeType.WaitUntilDate, node.Type);
    }

    [Fact]
    public void SubProcessNode_SetsSubProcessDefinition()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef);

        Assert.Equal(subDef, node.SubProcessDefinition);
        Assert.True(node.InheritAggregateId);
        Assert.Equal(NodeType.SubProcess, node.Type);
    }

    [Fact]
    public void DecisionNode_AddRoute_AddsConditionAndNextNode()
    {
        var node = new DecisionNode("CheckStatus");
        node.AddRoute("approved", "processApproval");
        node.AddRoute("rejected", "processRejection");

        Assert.Equal(2, node.ConditionToNodeId.Count);
        Assert.Equal("processApproval", node.ConditionToNodeId["approved"]);
        Assert.Equal("processRejection", node.ConditionToNodeId["rejected"]);
        Assert.Contains("processApproval", node.NextNodeIds);
        Assert.Contains("processRejection", node.NextNodeIds);
    }

    [Fact]
    public void DecisionNode_AddCondition_AddsConditionDecision()
    {
        var node = new DecisionNode();
        node.AddCondition("montant", 1000.0, OperateurFiltre.Superieur, TypeDonnee.Nombre, "highValue");

        Assert.Single(node.Conditions);
        Assert.Contains("highValue", node.NextNodeIds);
    }

    [Fact]
    public void DecisionNode_SetNoeudParDefaut()
    {
        var node = new DecisionNode("query");
        node.SetNoeudParDefaut("defaultNode");

        Assert.Equal("defaultNode", node.NoeudParDefaut);
        Assert.Contains("defaultNode", node.NextNodeIds);
    }

    [Fact]
    public void NodeExecutionResult_DefaultValues()
    {
        var result = new NodeExecutionResult();

        Assert.False(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Null(result.NextNodeId);
        Assert.Null(result.ErrorMessage);
    }
}
