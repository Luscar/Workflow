using SimpleBPM.Handlers;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests.Handlers;

public class WaitUntilDateNodeHandlerTests
{
    private readonly WaitUntilDateNodeHandler _handler = new();

    [Fact]
    public void NodeType_ReturnsWaitUntilDate()
    {
        Assert.Equal(NodeType.WaitUntilDate, _handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_FutureDate_StopsAndWaits()
    {
        var futureDate = DateTime.UtcNow.AddHours(1);
        var node = new WaitUntilDateNode(futureDate) { Name = "WaitTomorrow" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);
        Assert.Equal(node.Id, instance.CurrentNodeId);
        Assert.Equal(futureDate, instance.Variables["WaitUntilDate"]);
    }

    [Fact]
    public async Task HandleAsync_PastDate_ContinuesWithoutStopping()
    {
        var pastDate = DateTime.UtcNow.AddHours(-1);
        var node = new WaitUntilDateNode(pastDate) { Name = "AlreadyPassed" };
        node.NextNodeIds.Add("next-id");
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("next-id", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_DateFromProvider_UsesProviderResult()
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        var node = new WaitUntilDateNode(_ => futureDate) { Name = "ProviderDate" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);
    }

    [Fact]
    public async Task HandleAsync_DateFromVariables_UsesDateKey()
    {
        var futureDate = DateTime.UtcNow.AddDays(2);
        var node = new WaitUntilDateNode("ScheduledDate") { Name = "FromKey" };
        var instance = new ProcessInstance("proc-1");
        instance.Variables["ScheduledDate"] = futureDate;

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);
    }

    [Fact]
    public async Task HandleAsync_DateFromVariablesAsString_ParsesCorrectly()
    {
        var futureDate = DateTime.UtcNow.AddDays(3);
        var node = new WaitUntilDateNode("ScheduledDate") { Name = "FromStringKey" };
        var instance = new ProcessInstance("proc-1");
        instance.Variables["ScheduledDate"] = futureDate.ToString("O");

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.RequiresStop);
        Assert.Equal(ProcessStatus.WaitingDate, instance.Status);
    }

    [Fact]
    public async Task HandleAsync_PastDateFromVariables_ContinuesImmediately()
    {
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var node = new WaitUntilDateNode("ScheduledDate") { Name = "PastKey" };
        node.NextNodeIds.Add("after-wait");
        var instance = new ProcessInstance("proc-1");
        instance.Variables["ScheduledDate"] = pastDate;

        var result = await _handler.HandleAsync(node, instance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("after-wait", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_NoDateConfigured_ReturnsFailed()
    {
        var node = new WaitUntilDateNode() { Name = "NoDate" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No target date", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_DateKeyNotInVariables_ReturnsFailed()
    {
        var node = new WaitUntilDateNode("MissingKey") { Name = "MissingVar" };
        var instance = new ProcessInstance("proc-1");

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No target date", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_InvalidDateTypeInVariables_ReturnsFailed()
    {
        var node = new WaitUntilDateNode("BadDate") { Name = "BadType" };
        var instance = new ProcessInstance("proc-1");
        instance.Variables["BadDate"] = 12345;

        var result = await _handler.HandleAsync(node, instance);

        Assert.False(result.IsCompleted);
        Assert.Contains("No target date", result.ErrorMessage);
    }
}
