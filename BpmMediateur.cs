using SimpleBPM.Abstractions;

namespace SimpleBPM;

/// <summary>
/// Implémentation de <see cref="IBpmMediateur"/> qui dispatche vers des instances individuelles
/// de <see cref="IBpmCommandHandler"/> et <see cref="IBomQueryHandler"/>
/// résolues depuis le conteneur DI.
/// </summary>
public class BpmMediateur : IBpmMediateur
{
    private readonly Dictionary<string, IBpmCommandHandler> _commandHandlers;
    private readonly Dictionary<string, IBomQueryHandler> _queryHandlers;

    public BpmMediateur(
        IEnumerable<IBpmCommandHandler> commandHandlers,
        IEnumerable<IBomQueryHandler> queryHandlers)
    {
        _commandHandlers = commandHandlers.ToDictionary(h => h.CommandName);
        _queryHandlers = queryHandlers.ToDictionary(h => h.QueryName);
    }

    public Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        if (!_commandHandlers.TryGetValue(commandName, out var handler))
            throw new InvalidOperationException(
                $"Aucun IBpmCommandHandler enregistré pour la commande '{commandName}'.");

        return handler.HandleAsync(processId, aggregateId, parameters);
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        if (!_queryHandlers.TryGetValue(decisionName, out var handler))
            throw new InvalidOperationException(
                $"Aucun IBomQueryHandler enregistré pour la décision '{decisionName}'.");

        return handler.HandleAsync(processId, aggregateId, parameters);
    }
}
