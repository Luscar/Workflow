namespace SimpleBPM;

public interface IFlowService
{
    Task<Processus> ObtenirAsync(long instanceProcessId);
    Task<long> CreateProcessInstanceAsync(string definitionName, Dictionary<string, object>? variables = null);
    Task<List<Processus>> RechercherParVariableAsync(List<FiltreVariable> filtres);
    Task<List<Processus>> ObtenirEnfantsAsync(long idInstanceParent);
    Task<IEnumerable<string>> ObtenirSignauxEnAttenteAsync(long idInstanceProcessus);
    Task<InstanceNode> ObtenirNoeudAsync(long idInstanceNoeud);
    Task TerminerEtapeAsync(long idInstanceNoeud, object contenu);
    Task TerminerEtapeEnCoursAsync(long idInstanceProcessus, Dictionary<string, object>? contenu = null);
    Task EnvoyerSignalAsync(long idInstanceProcessus, string signalName);

    /// <summary>
    /// Sauvegarde une définition de processus en banque de données.
    /// Si le repository de définitions n'est pas configuré, une exception est levée.
    /// </summary>
    Task SaveDefinitionAsync(ProcessDefinition definition);

    /// <summary>
    /// Retourne toutes les définitions de processus disponibles (banque + mémoire).
    /// </summary>
    Task<List<ProcessDefinition>> GetDefinitionsAsync();
}
