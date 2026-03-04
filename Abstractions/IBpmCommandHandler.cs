namespace SimpleBPM.Abstractions;

/// <summary>
/// Traite une commande métier spécifique identifiée par <see cref="CommandName"/>.
/// Implémentez cette interface pour chaque commande dans le projet client ;
/// toutes les implémentations sont découvertes et enregistrées automatiquement
/// via <see cref="Localisation.RegistrationBpmModule.ScanHandlers"/>.
/// </summary>
public interface IBpmCommandHandler
{
    string CommandName { get; }
    Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null);
}
