using SimpleBPM.Abstractions;

namespace SimpleBPM;

/// <summary>
/// Implementation of <see cref="IBpmMediator"/> that dispatches to individual
/// <see cref="ICommandHandler"/> and <see cref="IQueryHandler"/> instances
/// resolved from the DI container.
/// </summary>
public class BpmMediator : IBpmMediator
{
    private readonly Dictionary<string, ICommandHandler> _commandHandlers;
    private readonly Dictionary<string, IQueryHandler> _queryHandlers;

    public BpmMediator(
        IEnumerable<ICommandHandler> commandHandlers,
        IEnumerable<IQueryHandler> queryHandlers)
    {
        _commandHandlers = commandHandlers.ToDictionary(h => h.CommandName);
        _queryHandlers = queryHandlers.ToDictionary(h => h.QueryName);
    }

    public Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        if (!_commandHandlers.TryGetValue(commandName, out var handler))
            throw new InvalidOperationException(
                $"No ICommandHandler registered for command '{commandName}'.");

        return handler.HandleAsync(processId, aggregateId, parameters);
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        if (!_queryHandlers.TryGetValue(decisionName, out var handler))
            throw new InvalidOperationException(
                $"No IQueryHandler registered for decision '{decisionName}'.");

        return handler.HandleAsync(processId, aggregateId, parameters);
    }
}
