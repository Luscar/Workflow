namespace SimpleBPM;

/// <summary>
/// Read-only monitoring interface for observing process definitions and instances.
/// </summary>
public interface IProcessMonitor
{
    /// <summary>
    /// Returns all registered process definitions.
    /// </summary>
    List<ProcessDefinition> GetDefinitions();

    /// <summary>
    /// Returns all process instances.
    /// </summary>
    Task<List<Processus>> GetAllInstancesAsync();

    /// <summary>
    /// Returns process instances filtered by status.
    /// </summary>
    Task<List<Processus>> GetInstancesByStatusAsync(ProcessStatus status);

    /// <summary>
    /// Returns a single process instance with its details.
    /// </summary>
    Task<Processus> GetInstanceAsync(long processId);

    /// <summary>
    /// Returns the execution history for a process instance.
    /// </summary>
    Task<List<NodeInstance>> GetExecutionHistoryAsync(long processId);

    /// <summary>
    /// Returns count of instances grouped by status.
    /// </summary>
    Task<Dictionary<ProcessStatus, int>> GetStatusSummaryAsync();
}
