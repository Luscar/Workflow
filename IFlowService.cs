namespace SimpleBPM;

public interface IFlowService
{
    Task<Processus> ObtenirAsync(long instanceProcessId);
    Task<long> CreateProcessInstance(string definitionName, Dictionary<string, object>? variables = null);

    /// <summary>
    /// Creates a new process instance, or returns the ID of the existing one if a process
    /// with <paramref name="idempotencyKey"/> was already created. Use this when the messaging
    /// layer delivers at-least-once and you need process-creation deduplication.
    /// </summary>
    Task<long> CreateProcessInstanceIdempotentAsync(string idempotencyKey, string definitionName, Dictionary<string, object>? variables = null);
    Task<List<Processus>> RechercherParVariable(List<FiltreVariable> filtres);
    Task<List<Processus>> ObtenirEnfants(long idInstanceParent);
    Task<IEnumerable<string>> ObtenirSignauxEnAttente(long idInstanceProcessus);
    Task<InstanceNode> Obtenir(long idInstanceNoeud);
    Task TerminerEtape(long idInstanceNoeud, object contenu);
    Task TerminerEtapeEnCours(long idInstanceProcessus, Dictionary<string, object>? contenu = null);
    Task EnvoyerSignalAsync(long idInstanceProcessus, string signalName);
}
