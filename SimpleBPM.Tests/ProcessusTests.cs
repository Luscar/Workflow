using SimpleBPM;

namespace SimpleBPM.Tests;

public class ProcessusTests
{
    [Fact]
    public void FromInstance_MapsAllProperties()
    {
        var instance = new ProcessInstance(42, "AGG-001")
        {
            DefinitionName = "MyProcess",
            DefinitionVersion = "2.0",
            Status = ProcessStatus.Running,
            ErrorMessage = null,
            CurrentNodeId = "step1"
        };
        instance.Variables["key1"] = "value1";
        instance.CompletedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        var processus = Processus.FromInstance(instance);

        Assert.Equal(42, processus.Id);
        Assert.Equal("AGG-001", processus.AggregateId);
        Assert.Equal("MyProcess", processus.DefinitionName);
        Assert.Equal("2.0", processus.DefinitionVersion);
        Assert.Equal(ProcessStatus.Running, processus.Status);
        Assert.Null(processus.ErrorMessage);
        Assert.Equal("step1", processus.CurrentNodeId);
        Assert.Equal("value1", processus.Variables["key1"]);
        Assert.Equal(instance.StartedAt, processus.StartedAt);
        Assert.Equal(instance.CompletedAt, processus.CompletedAt);
    }

    [Fact]
    public void FromInstance_CopiesVariables_NotReference()
    {
        var instance = new ProcessInstance(1);
        instance.Variables["key"] = "original";

        var processus = Processus.FromInstance(instance);
        processus.Variables["key"] = "modified";

        Assert.Equal("original", instance.Variables["key"]);
    }
}
