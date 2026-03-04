namespace SimpleBPM;

public interface IFlowService
{
    Task<Processus> ObtenirAsync(long instanceProcessId);
    Task<long> CreateProcessInstanceAsync(string definitionName, Dictionary<string, object>? variables = null);
    Task<List<Processus>> RechercherParVariableAsync(List<FiltreVariable> filtres);
    Task<List<Processus>> ObtenirEnfantsAsync(long idInstanceParent);
    Task<IEnumerable<string>> ObtenirSignauxEnAttenteAsync(long idInstanceProcessus);
    Task<NoeudProcessus> ObtenirNoeudAsync(long idInstanceNoeud);
    Task TerminerEtapeAsync(long idInstanceNoeud, object contenu);
    Task TerminerEtapeEnCoursAsync(long idInstanceProcessus, Dictionary<string, object>? contenu = null);
    Task EnvoyerSignalAsync(long idInstanceProcessus, string signalName);

    /// <summary>
    /// Sauvegarde une définition de processus dans la banque de définitions.
    /// Requiert qu'une <see cref="Persistence.IDefinitionRepository"/> soit configurée.
    /// </summary>
    Task SauvegarderDefinitionAsync(ProcessDefinition definition);

    /// <summary>
    /// Retourne toutes les définitions disponibles dans la banque.
    /// Requiert qu'une <see cref="Persistence.IDefinitionRepository"/> soit configurée.
    /// </summary>
    Task<List<ProcessDefinition>> ObtenirDefinitionsAsync();
}
