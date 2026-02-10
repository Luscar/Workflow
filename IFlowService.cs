namespace SimpleBPM;

public interface IFlowService
{
    Task<Processus> ObtenirAsync(long instanceProcessId);
    Task<long> CreateProcessInstance(string definitionName, Dictionary<string, object>? variables = null);
    Task<List<Processus>> RechercherParVariable(List<FiltreVariable> filtres);
    Task<List<Processus>> ObtenirEnfants(long idInstanceParent);
    Task<IEnumerable<string>> ObtenirSignauxEnAttente(long idInstanceProcessus);
    Task<InstanceNode> Obtenir(long idInstanceNoeud);
    Task TerminerEtape(long idInstanceNoeud, object contenu);
    Task TerminerEtapeEnCours(long idInstanceProcessus, Dictionary<string, object>? contenu = null);
    Task EnvoyerSignalAsync(long idInstanceProcessus, string signalName);
}
