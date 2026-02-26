using SimpleBPM;

namespace SimpleBPM.Tests;

public class ProcessInstanceTests
{
    [Fact]
    public void Constructor_SetsProcessIdAndDefaults()
    {
        var instance = new ProcessInstance(42);

        Assert.Equal(42, instance.ProcessId);
        Assert.Null(instance.AggregateId);
        Assert.Equal(ProcessStatus.Running, instance.Status);
        Assert.NotNull(instance.Variables);
        Assert.Empty(instance.Variables);
        Assert.Null(instance.WaitDate);
        Assert.Null(instance.ExpectedSignal);
        Assert.NotNull(instance.ExecutionHistory);
        Assert.Empty(instance.ExecutionHistory);
    }

    [Fact]
    public void Constructor_WithAggregateId()
    {
        var instance = new ProcessInstance(1, 1L);

        Assert.Equal(1, instance.ProcessId);
        Assert.Equal(1L, instance.AggregateId);
    }

    [Fact]
    public void StartedAt_IsSetOnConstruction()
    {
        var before = DateTime.UtcNow;
        var instance = new ProcessInstance(1);
        var after = DateTime.UtcNow;

        Assert.InRange(instance.StartedAt, before, after);
    }

    [Fact]
    public void TotalDuration_IsNull_WhenNotCompleted()
    {
        var instance = new ProcessInstance(1);
        Assert.Null(instance.TotalDuration);
    }

    [Fact]
    public void TotalDuration_ReturnsValue_WhenCompleted()
    {
        var instance = new ProcessInstance(1);
        instance.CompletedAt = instance.StartedAt.AddMinutes(5);

        Assert.NotNull(instance.TotalDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), instance.TotalDuration.Value);
    }

    [Fact]
    public void CurrentDuration_ReturnsPositiveValue()
    {
        var instance = new ProcessInstance(1);
        Assert.True(instance.CurrentDuration >= TimeSpan.Zero);
    }

    [Fact]
    public void CompletedStepsCount_CountsSuccessfulEntries()
    {
        var instance = new ProcessInstance(1);
        var h1 = new NodeInstance("n1", "Node 1", NodeType.Business);
        h1.Complete(true);
        var h2 = new NodeInstance("n2", "Node 2", NodeType.Business);
        h2.Complete(false, "Error");
        var h3 = new NodeInstance("n3", "Node 3", NodeType.Business);
        h3.Complete(true);

        instance.ExecutionHistory.Add(h1);
        instance.ExecutionHistory.Add(h2);
        instance.ExecutionHistory.Add(h3);

        Assert.Equal(2, instance.CompletedStepsCount);
    }

    [Fact]
    public void FailedStepsCount_CountsFailedEntries()
    {
        var instance = new ProcessInstance(1);
        var h1 = new NodeInstance("n1", "Node 1", NodeType.Business);
        h1.Complete(true);
        var h2 = new NodeInstance("n2", "Node 2", NodeType.Business);
        h2.Complete(false, "Error");

        instance.ExecutionHistory.Add(h1);
        instance.ExecutionHistory.Add(h2);

        Assert.Equal(1, instance.FailedStepsCount);
    }

    [Fact]
    public void Variables_CanBeSetAndRetrieved()
    {
        var instance = new ProcessInstance(1);
        instance.Variables["key1"] = "value1";
        instance.Variables["key2"] = 42;

        Assert.Equal("value1", instance.Variables["key1"]);
        Assert.Equal(42, instance.Variables["key2"]);
    }

    [Fact]
    public void ExpectedSignal_CanBeSetAndRetrieved()
    {
        var instance = new ProcessInstance(1);
        instance.ExpectedSignal = "approval";

        Assert.Equal("approval", instance.ExpectedSignal);
    }

    [Fact]
    public void ParentProcess_Properties()
    {
        var instance = new ProcessInstance(10)
        {
            ParentProcessId = 5,
            ParentNodeName = "subNodeDefinition"
        };

        Assert.Equal(5, instance.ParentProcessId);
        Assert.Equal("subNodeDefinition", instance.ParentNodeName);
    }
}
