namespace SimpleBPM;

/// <summary>
/// Interface de surveillance en lecture seule pour observer les définitions et instances de processus.
/// </summary>
public interface IProcessMonitor
{
    /// <summary>
    /// Retourne toutes les définitions de processus enregistrées.
    /// </summary>
    List<ProcessDefinition> GetDefinitions();

    /// <summary>
    /// Retourne toutes les instances de processus.
    /// </summary>
    Task<List<Processus>> GetAllInstancesAsync();

    /// <summary>
    /// Retourne les instances de processus filtrées par statut.
    /// </summary>
    Task<List<Processus>> GetInstancesByStatusAsync(ProcessStatus status);

    /// <summary>
    /// Retourne une instance de processus unique avec ses détails.
    /// </summary>
    Task<Processus> GetInstanceAsync(long processId);

    /// <summary>
    /// Retourne l'historique d'exécution d'une instance de processus.
    /// </summary>
    Task<List<NodeInstance>> GetExecutionHistoryAsync(long processId);

    /// <summary>
    /// Retourne le nombre d'instances regroupées par statut.
    /// </summary>
    Task<Dictionary<ProcessStatus, int>> GetStatusSummaryAsync();
}
