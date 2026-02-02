using SimpleBPM.Nodes;

namespace SimpleBPM.Visualization.Tests;

public class MermaidDiagramGeneratorTests
{
    private readonly MermaidDiagramGenerator _generator = new();

    [Fact]
    public void Format_ReturnsMermaid()
    {
        Assert.Equal("Mermaid", _generator.Format);
    }

    [Fact]
    public void Generate_LinearProcess_ContainsFlowchartHeader()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("flowchart TD", result);
    }

    [Fact]
    public void Generate_WithTitle_ContainsTitleBlock()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowTitle = true });

        Assert.Contains("title: LinearProcess", result);
    }

    [Fact]
    public void Generate_WithoutTitle_OmitsTitleBlock()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowTitle = false });

        Assert.DoesNotContain("title:", result);
    }

    [Fact]
    public void Generate_LeftToRight_UsesLRDirection()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { Direction = DiagramDirection.LeftToRight });

        Assert.Contains("flowchart LR", result);
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

        // Should have an edge between the two nodes
        Assert.Contains("-->", result);
    }

    [Fact]
    public void Generate_DecisionProcess_ContainsConditionLabels()
    {
        var definition = TestProcessFactory.CreateDecisionProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("in_stock", result);
        Assert.Contains("out_of_stock", result);
    }

    [Fact]
    public void Generate_DecisionProcess_WithoutConditionLabels_OmitsLabels()
    {
        var definition = TestProcessFactory.CreateDecisionProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowConditionLabels = false });

        // Should still have edges but without condition labels in pipe notation
        Assert.DoesNotContain("|\"in_stock\"|", result);
    }

    [Fact]
    public void Generate_DecisionNode_UsesDiamondShape()
    {
        var definition = TestProcessFactory.CreateDecisionProcess();
        var result = _generator.Generate(definition);

        // Mermaid diamond: {" ... "}
        Assert.Contains("{\"", result);
    }

    [Fact]
    public void Generate_AllNodeTypes_ContainsAllNodeStyles()
    {
        var definition = TestProcessFactory.CreateAllNodeTypesProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("businessNode", result);
        Assert.Contains("decisionNode", result);
        Assert.Contains("interactiveNode", result);
        Assert.Contains("waitNode", result);
        Assert.Contains("subProcessNode", result);
    }

    [Fact]
    public void Generate_WithNodeDetails_ContainsCommandNames()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowNodeDetails = true });

        Assert.Contains("ValidateOrder", result);
        Assert.Contains("ProcessOrder", result);
    }

    [Fact]
    public void Generate_WithoutNodeDetails_OmitsCommandNames()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowNodeDetails = false });

        // Should have node names but not the small command details
        Assert.Contains("Validate Order", result);
        Assert.DoesNotContain("<small>ValidateOrder</small>", result);
    }

    [Fact]
    public void Generate_ContainsStartMarker()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("start(( ))", result);
        Assert.Contains("startNode", result);
    }

    [Fact]
    public void Generate_ContainsStyleClassDefinitions()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition);

        Assert.Contains("classDef businessNode", result);
        Assert.Contains("classDef decisionNode", result);
        Assert.Contains("classDef interactiveNode", result);
        Assert.Contains("classDef waitNode", result);
        Assert.Contains("classDef subProcessNode", result);
    }

    [Fact]
    public void Generate_WithLegend_ContainsLegendSubgraph()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowLegend = true });

        Assert.Contains("subgraph Legend", result);
    }

    [Fact]
    public void Generate_WithoutLegend_OmitsLegendSubgraph()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = _generator.Generate(definition, new DiagramOptions { ShowLegend = false });

        Assert.DoesNotContain("subgraph Legend", result);
    }

    // === Instance state tests ===

    [Fact]
    public void Generate_WithInstance_ContainsInstanceStyles()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("classDef currentNode", result);
        Assert.Contains("classDef completedNode", result);
        Assert.Contains("classDef failedNode", result);
    }

    [Fact]
    public void Generate_PartiallyExecuted_MarksCurrentNode()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("currentNode", result);
    }

    [Fact]
    public void Generate_PartiallyExecuted_MarksCompletedNodes()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("completedNode", result);
    }

    [Fact]
    public void Generate_CompletedProcess_NoCurrentNodeHighlight()
    {
        var (definition, instance) = TestProcessFactory.CreateCompletedProcess();
        var result = _generator.Generate(definition, instance);

        // All nodes should be completed, not current
        // The class assignments should not include "currentNode" for any node
        var lines = result.Split('\n');
        var classLines = lines.Where(l => l.TrimStart().StartsWith("class n") && l.Contains("currentNode"));
        Assert.Empty(classLines);
    }

    [Fact]
    public void Generate_FailedProcess_MarksFailedNode()
    {
        var (definition, instance) = TestProcessFactory.CreateFailedProcess();
        var result = _generator.Generate(definition, instance);

        Assert.Contains("failedNode", result);
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

    [Fact]
    public void Generate_ProducesValidMermaidSyntax()
    {
        // Verify the output has the expected structural elements
        var definition = TestProcessFactory.CreateDecisionProcess();
        var result = _generator.Generate(definition);

        // Should start with frontmatter or flowchart
        Assert.True(result.StartsWith("---") || result.StartsWith("flowchart"));
        // Should contain flowchart directive
        Assert.Contains("flowchart", result);
        // Should contain at least one edge
        Assert.Contains("-->", result);
    }
}
