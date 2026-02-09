using SimpleBPM;

namespace SimpleBPM.Tests;

public class ProcessDefinitionTests
{
    [Fact]
    public void Constructor_SetsNameAndVersion()
    {
        var def = new ProcessDefinition("TestProcess", "2.0");

        Assert.Equal("TestProcess", def.Name);
        Assert.Equal("2.0", def.Version);
    }

    [Fact]
    public void Constructor_DefaultVersion()
    {
        var def = new ProcessDefinition("TestProcess");

        Assert.Equal("1.0", def.Version);
    }

    [Fact]
    public void AddNode_AddsNodeToDefinition()
    {
        var def = new ProcessDefinition("Test");
        var node = new ProcessNode(NodeType.Business) { Name = "step1" };

        def.AddNode(node);

        Assert.Single(def.Nodes);
        Assert.True(def.Nodes.ContainsKey("step1"));
    }

    [Fact]
    public void AddNode_FirstNode_BecomesStartNode()
    {
        var def = new ProcessDefinition("Test");
        var node = new ProcessNode(NodeType.Business) { Name = "first" };

        def.AddNode(node);

        Assert.Equal("first", def.StartNodeId);
    }

    [Fact]
    public void AddNode_ReturnsSelf_ForChaining()
    {
        var def = new ProcessDefinition("Test");
        var node = new ProcessNode(NodeType.Business) { Name = "step1" };

        var result = def.AddNode(node);

        Assert.Same(def, result);
    }

    [Fact]
    public void SetStartNode_ChangesStartNodeId()
    {
        var def = new ProcessDefinition("Test");
        def.AddNode(new ProcessNode(NodeType.Business) { Name = "step1" });
        def.AddNode(new ProcessNode(NodeType.Business) { Name = "step2" });

        def.SetStartNode("step2");

        Assert.Equal("step2", def.StartNodeId);
    }

    [Fact]
    public void SetStartNode_ThrowsForNonExistentNode()
    {
        var def = new ProcessDefinition("Test");
        def.AddNode(new ProcessNode(NodeType.Business) { Name = "step1" });

        Assert.Throws<ArgumentException>(() => def.SetStartNode("nonexistent"));
    }

    [Fact]
    public void GetNode_ReturnsExistingNode()
    {
        var def = new ProcessDefinition("Test");
        var node = new ProcessNode(NodeType.Business) { Name = "step1" };
        def.AddNode(node);

        var result = def.GetNode("step1");

        Assert.NotNull(result);
        Assert.Same(node, result);
    }

    [Fact]
    public void GetNode_ReturnsNull_ForNonExistentNode()
    {
        var def = new ProcessDefinition("Test");

        var result = def.GetNode("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void AddNode_MultipleNodes()
    {
        var def = new ProcessDefinition("Test");
        def.AddNode(new ProcessNode(NodeType.Business) { Name = "step1" });
        def.AddNode(new ProcessNode(NodeType.Interactive) { Name = "step2" });
        def.AddNode(new ProcessNode(NodeType.Decision) { Name = "step3" });

        Assert.Equal(3, def.Nodes.Count);
    }
}
