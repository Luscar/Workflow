namespace SimpleBPM.Abstractions;

/// <summary>
/// Handles a specific business command identified by <see cref="CommandName"/>.
/// Implement this interface for each command in the client project;
/// all implementations are discovered and registered automatically
/// via <see cref="Localisation.ServiceCollectionExtensions.AddCommandHandlers"/>.
/// </summary>
public interface ICommandHandler
{
    string CommandName { get; }
    Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
}
