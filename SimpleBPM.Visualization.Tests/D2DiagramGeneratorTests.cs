using SimpleBPM.Nodes;

namespace SimpleBPM.Visualization.Tests;

public class D2DiagramGeneratorTests
{
    private readonly D2DiagramGenerator _generator = new();

    [Fact]
    public void Format_ReturnsD2()
    {
        Assert.Equal("D2", _generator.Format);
    }

    [Fact]
    public void Generate_LinearProcess_ContainsDirectionDown()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("direction: down", result);
    }

    [Fact]
    public void Generate_LeftToRight_ContainsDirectionRight()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { Direction = DiagramDirection.LeftToRight });

        Assert.Contains("direction: right", result);
    }

    [Fact]
    public void Generate_WithTitle_ContainsTitleBlock()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowTitle = true });

        Assert.Contains("title: \"LinearProcess\"", result);
    }

    [Fact]
    public void Generate_WithoutTitle_OmitsTitleBlock()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowTitle = false });

        Assert.DoesNotContain("title:", result);
    }

    [Fact]
    public void Generate_LinearProcess_ContainsNodeNames()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("Validate Order", result);
        Assert.Contains("Process Order", result);
    }

    [Fact]
    public void Generate_LinearProcess_ContainsEdges()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("->", result);
    }

    [Fact]
    public void Generate_DecisionProcess_ContainsConditionLabels()
    {
        var definition = TestProcessFactory.CreateDecisionProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("\"in_stock\"", result);
        Assert.Contains("\"out_of_stock\"", result);
    }

    [Fact]
    public void Generate_DecisionNode_UsesDiamondShape()
    {
        var definition = TestProcessFactory.CreateDecisionProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("shape: diamond", result);
    }

    [Fact]
    public void Generate_BusinessNode_UsesRectangleShape()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("shape: rectangle", result);
    }

    [Fact]
    public void Generate_InteractiveNode_UsesParallelogramShape()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("shape: parallelogram", result);
    }

    [Fact]
    public void Generate_WaitNode_UsesOvalShape()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("shape: oval", result);
    }

    [Fact]
    public void Generate_SubProcessNode_UsesPageShape()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("shape: page", result);
    }

    [Fact]
    public void Generate_AllNodeTypes_ContainsAllClasses()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("class: business", result);
        Assert.Contains("class: decision", result);
        Assert.Contains("class: interactive", result);
        Assert.Contains("class: wait", result);
        Assert.Contains("class: subprocess", result);
    }

    [Fact]
    public void Generate_ContainsClassDefinitions()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("classes: {", result);
        Assert.Contains("business: {", result);
    }

    [Fact]
    public void Generate_ContainsStartCircle()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("start:", result);
        Assert.Contains("shape: circle", result);
        Assert.Contains("start ->", result);
    }

    [Fact]
    public void Generate_WithNodeDetails_ContainsCommandNames()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowNodeDetails = true });

        Assert.Contains("ValidateOrder", result);
    }

    [Fact]
    public void Generate_WithoutNodeDetails_OmitsCommandNames()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowNodeDetails = false });

        Assert.Contains("Validate Order", result);
        Assert.DoesNotContain("ValidateOrder", result);
    }

    [Fact]
    public void Generate_WithLegend_ContainsLegendBlock()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowLegend = true });

        Assert.Contains("legend:", result);
    }

    [Fact]
    public void Generate_WaitForSignalNode_ContainsSignalName()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("PaymentReceived", result);
    }

    [Fact]
    public void Generate_SubProcessNode_ContainsSubProcessName()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("SubValidation", result);
    }

    // === Instance state tests ===

    [Fact]
    public void Generate_WithInstance_ContainsInstanceClasses()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("current: {", result);
        Assert.Contains("completed: {", result);
        Assert.Contains("failed: {", result);
    }

    [Fact]
    public void Generate_PartiallyExecuted_MarksCurrentNode()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("class: current", result);
    }

    [Fact]
    public void Generate_PartiallyExecuted_MarksCompletedNodes()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("class: completed", result);
    }

    [Fact]
    public void Generate_CompletedProcess_NoCurrentNodeClass()
    {
        var (definition, instance) = TestProcessFactory.CreateCompletedProcess();
        var result = _generator.Generate(definition, instance);

        // Completed process should not have "class: current" on any node
        var lines = result.Split('\n');
        var currentAssignments = lines.Count(l => l.Trim() == "class: current");
        Assert.Equal(0, currentAssignments);
    }

    [Fact]
    public void Generate_FailedProcess_MarksFailedNode()
    {
        var (definition, instance) = TestProcessFactory.CreateFailedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("class: failed", result);
    }

    [Fact]
    public void Generate_DefinitionOnly_NoInstanceClasses()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.DoesNotContain("class: current", result);
        Assert.DoesNotContain("class: completed", result);
        Assert.DoesNotContain("class: failed", result);
        Assert.DoesNotContain("current: {", result);
    }
}
