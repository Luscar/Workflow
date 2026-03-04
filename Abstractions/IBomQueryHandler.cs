namespace SimpleBPM.Abstractions;

/// <summary>
/// Traite une requête de décision spécifique identifiée par <see cref="QueryName"/>.
/// Implémentez cette interface pour chaque décision dans le projet client ;
/// toutes les implémentations sont découvertes et enregistrées automatiquement
/// via <see cref="Localisation.ServiceCollectionExtensions.AddCommandHandlers"/>.
/// </summary>
public interface IBomQueryHandler
{
    string QueryName { get; }
    Task<string> HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
}
