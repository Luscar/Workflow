using SimpleBPM;

namespace SimpleBPM.Tests;

public class NodeInstanceTests
{
    [Fact]
    public void Constructor_SetsInitialValues()
    {
        var before = DateTime.UtcNow;
        var history = new NodeInstance("step1", "Étape 1", NodeType.Business);
        var after = DateTime.UtcNow;

        Assert.Equal("step1", history.NodeId);
        Assert.Equal("Étape 1", history.NodeName);
        Assert.Equal(NodeType.Business, history.NodeType);
        Assert.InRange(history.StartedAt, before, after);
    }

    [Fact]
    public void Complete_Success_SetsProperties()
    {
        var history = new NodeInstance("step1", "Step 1", NodeType.Business);
        history.Complete(true, null, "step2");

        Assert.True(history.Success);
        Assert.Null(history.ErrorMessage);
        Assert.Equal("step2", history.NextNodeId);
        Assert.True(history.CompletedAt >= history.StartedAt);
    }

    [Fact]
    public void Complete_Failure_SetsErrorMessage()
    {
        var history = new NodeInstance("step1", "Step 1", NodeType.Business);
        history.Complete(false, "Something went wrong");

        Assert.False(history.Success);
        Assert.Equal("Something went wrong", history.ErrorMessage);
    }

    [Fact]
    public void Duration_ReturnsTimeBetweenStartAndComplete()
    {
        var history = new NodeInstance("step1", "Step 1", NodeType.Business);
        history.Complete(true);

        Assert.True(history.Duration >= TimeSpan.Zero);
    }
}
