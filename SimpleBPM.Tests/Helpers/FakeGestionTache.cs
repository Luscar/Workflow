using SimpleBPM.Abstractions;

namespace SimpleBPM.Tests.Helpers;

public class FakeGestionTache : IGestionTache
{
    private readonly List<string> _createdTasks = new();
    private readonly List<string> _closedTasks = new();

    public IReadOnlyList<string> CreatedTasks => _createdTasks;
    public IReadOnlyList<string> ClosedTasks => _closedTasks;

    public Task CreerTacheAsync(string processId, string? aggregateId, string definitionName, string nodeName)
    {
        _createdTasks.Add(nodeName);
        return Task.CompletedTask;
    }

    public Task FermerTacheAsync(string processId, string? aggregateId, string definitionName, string nodeName)
    {
        _closedTasks.Add(nodeName);
        return Task.CompletedTask;
    }
}
