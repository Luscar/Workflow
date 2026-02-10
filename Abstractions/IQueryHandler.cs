namespace SimpleBPM.Abstractions;

/// <summary>
/// Handles a specific decision query identified by <see cref="QueryName"/>.
/// Implement this interface for each decision in the client project;
/// all implementations are discovered and registered automatically
/// via <see cref="Localisation.ServiceCollectionExtensions.AddCommandHandlers"/>.
/// </summary>
public interface IQueryHandler
{
    string QueryName { get; }
    Task<string> HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
}
