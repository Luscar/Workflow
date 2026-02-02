namespace SimpleBPM.Visualization.Tests;

public class ProcessDiagramExtensionsTests
{
    [Fact]
    public void ToMermaid_ReturnsValidMermaidOutput()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = definition.ToMermaid();

        Assert.Contains("flowchart", result);
        Assert.Contains("Validate Order", result);
    }

    [Fact]
    public void ToMermaid_WithInstance_ReturnsInstanceState()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = definition.ToMermaid(instance);

        Assert.Contains("currentNode", result);
    }

    [Fact]
    public void ToMermaid_WithOptions_RespectsOptions()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = definition.ToMermaid(new DiagramOptions { Direction = DiagramDirection.LeftToRight });

        Assert.Contains("flowchart LR", result);
    }

    [Fact]
    public void ToD2_ReturnsValidD2Output()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = definition.ToD2();

        Assert.Contains("direction: down", result);
        Assert.Contains("Validate Order", result);
    }

    [Fact]
    public void ToD2_WithInstance_ReturnsInstanceState()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var result = definition.ToD2(instance);

        Assert.Contains("class: current", result);
    }

    [Fact]
    public void ToD2_WithOptions_RespectsOptions()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var result = definition.ToD2(new DiagramOptions { Direction = DiagramDirection.LeftToRight });

        Assert.Contains("direction: right", result);
    }

    [Fact]
    public void WriteDiagramToFile_MermaidExtension_WritesMermaid()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var tempFile = Path.GetTempFileName() + ".mmd";

        try
        {
            definition.WriteDiagramToFile(tempFile);
            var content = File.ReadAllText(tempFile);
            Assert.Contains("flowchart", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteDiagramToFile_D2Extension_WritesD2()
    {
        var definition = TestProcessFactory.CreateLinearProcess();
        var tempFile = Path.GetTempFileName() + ".d2";

        try
        {
            definition.WriteDiagramToFile(tempFile);
            var content = File.ReadAllText(tempFile);
            Assert.Contains("direction: down", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteDiagramToFile_WithInstance_IncludesInstanceState()
    {
        var (definition, instance) = TestProcessFactory.CreatePartiallyExecutedProcess();
        var tempFile = Path.GetTempFileName() + ".mmd";

        try
        {
            definition.WriteDiagramToFile(tempFile, instance);
            var content = File.ReadAllText(tempFile);
            Assert.Contains("currentNode", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
