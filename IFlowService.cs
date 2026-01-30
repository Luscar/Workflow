namespace SimpleBPM;

public interface IFlowService
{
    Task<Processus> ObtenirAsync(string instanceProcessId);
    Task<string> CreateProcessInstance(string definitionId, Dictionary<string, object>? variables = null);
    Task<List<Processus>> RechercherParVariable(Dictionary<string, object> variablesFiltre);
    Task<List<Processus>> ObtenirEnfants(string idInstanceParent);
    Task<Processus?> ObtenirParent(string idInstanceEnfant);
    Task<IEnumerable<string>> ObtenirSignauxEnAttente(string idInstanceProcessus);
    Task<InstanceNode> Obtenir(string idInstanceNoeud);
    Task TerminerEtape(string idInstanceNoeud, object contenu);
}
