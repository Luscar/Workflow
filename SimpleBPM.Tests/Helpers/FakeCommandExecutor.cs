using SimpleBPM.Abstractions;

namespace SimpleBPM.Tests.Helpers;

public class FakeCommandExecutor : ICommandExecutor
{
    private readonly Dictionary<string, string> _decisionResults = new();
    private readonly List<string> _executedCommands = new();
    private readonly List<string> _evaluatedDecisions = new();
    private Func<string, Task>? _onExecute;

    public IReadOnlyList<string> ExecutedCommands => _executedCommands;
    public IReadOnlyList<string> EvaluatedDecisions => _evaluatedDecisions;

    public FakeCommandExecutor WithDecisionResult(string decisionName, string result)
    {
        _decisionResults[decisionName] = result;
        return this;
    }

    public FakeCommandExecutor WithOnExecute(Func<string, Task> onExecute)
    {
        _onExecute = onExecute;
        return this;
    }

    public async Task ExecuteCommandAsync(string commandName, string processId, string? aggregateId)
    {
        _executedCommands.Add(commandName);
        if (_onExecute != null)
            await _onExecute(commandName);
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, string processId, string? aggregateId)
    {
        _evaluatedDecisions.Add(decisionName);
        var result = _decisionResults.GetValueOrDefault(decisionName, "default");
        return Task.FromResult(result);
    }
}
