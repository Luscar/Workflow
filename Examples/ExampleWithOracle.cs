using Oracle.ManagedDataAccess.Client;
using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;
using SimpleBPM.Handlers;
using SimpleBPM.Persistence;

// Configuration Oracle avec préfixe de table
var oracleConfig = new OracleConfiguration(
    connectionString: "User Id=myuser;Password=mypass;Data Source=localhost:1521/XEPDB1",
    tablePrefix: "ABC" // Préfixe de 3 à 10 lettres
);

// Connexion gérée par le client
using var connection = new OracleConnection(oracleConfig.ConnectionString);
connection.Open();

// Créer le repository avec la connexion
var repository = new OracleProcessRepository(oracleConfig, connection);

// Initialiser la base de données (créer les tables si nécessaire)
await repository.InitializeDatabaseAsync();

// Configuration des handlers
var executor = new SampleExecutor();
var handlers = new INodeHandler[]
{
    new BusinessNodeHandler(executor),
    new DecisionNodeHandler(executor)
};

// Définition du processus
var processDefinition = new ProcessDefinition("OrderProcess");

var validateOrderNode = new BusinessNode("ValidateOrder") { Name = "ValidateOrder", DisplayName = "Validate Order" };
var checkInventoryNode = new BusinessNode("CheckInventory") { Name = "CheckInventory", DisplayName = "Check Inventory" };
var decisionNode = new DecisionNode("DecideApproval") { Name = "DecideApproval", DisplayName = "Approval Decision" };
var approvedNode = new BusinessNode("ProcessApprovedOrder") { Name = "ProcessApproved", DisplayName = "Process Approved" };
var rejectedNode = new BusinessNode("ProcessRejectedOrder") { Name = "ProcessRejected", DisplayName = "Process Rejected" };
var waitNode = new WaitForSignalNode("PaymentReceived") { Name = "WaitPayment", DisplayName = "Wait Payment" };
var interactiveNode = new InteractiveNode() { Name = "ManualReview", DisplayName = "Manual Review" };

validateOrderNode.NextNodeIds.Add(checkInventoryNode.Name);
checkInventoryNode.NextNodeIds.Add(decisionNode.Name);

decisionNode
    .AddRoute("approved", approvedNode.Name)
    .AddRoute("rejected", rejectedNode.Name);

approvedNode.NextNodeIds.Add(waitNode.Name);
waitNode.NextNodeIds.Add(interactiveNode.Name);

processDefinition
    .AddNode(validateOrderNode)
    .AddNode(checkInventoryNode)
    .AddNode(decisionNode)
    .AddNode(approvedNode)
    .AddNode(rejectedNode)
    .AddNode(waitNode)
    .AddNode(interactiveNode);

// Créer le moteur avec le repository et les handlers
var engine = new FlowEngine(new[] { processDefinition }, repository, handlers);

// Démarrer un nouveau processus
var instance = new ProcessInstance(123, "aggregate-456");
instance = await engine.ExecuteAsync(instance);
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeName}");
// L'instance est automatiquement sauvegardée dans Oracle avec le préfixe ABC_PROCESS_CONTEXT

// Historique d'exécution détaillé
Console.WriteLine("\n=== Historique d'exécution ===");
foreach (var history in instance.ExecutionHistory)
{
    Console.WriteLine($"Nœud: {history.NodeName} ({history.NodeType})");
    Console.WriteLine($"  Début: {history.StartedAt:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"  Fin: {history.CompletedAt:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"  Durée: {history.Duration.TotalMilliseconds:F2} ms");
    Console.WriteLine($"  Succès: {history.Success}");
    if (!string.IsNullOrEmpty(history.ErrorMessage))
    {
        Console.WriteLine($"  Erreur: {history.ErrorMessage}");
    }
    Console.WriteLine($"  Prochain nœud: {history.NextNodeId ?? "Aucun"}");
    Console.WriteLine();
}

// Envoyer un signal
instance = await engine.SignalAsync(instance, "PaymentReceived");
Console.WriteLine("=== Après signal ===");
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Étapes exécutées: {instance.ExecutionHistory.Count}");

// Continuer l'exécution
instance = await engine.ContinueAsync(instance);
Console.WriteLine($"\n=== Final ===");
Console.WriteLine($"Status: {instance.Status}");

// Plus tard, charger le processus depuis la base de données
var loadedInstance = await engine.LoadProcessAsync(123);
if (loadedInstance != null)
{
    Console.WriteLine("\n=== Instance rechargée depuis Oracle ===");
    Console.WriteLine($"Process ID: {loadedInstance.ProcessId}");
    Console.WriteLine($"Démarré le: {loadedInstance.StartedAt}");
    Console.WriteLine($"Dernière exécution: {loadedInstance.LastExecutedAt}");
    if (loadedInstance.CompletedAt.HasValue)
    {
        Console.WriteLine($"Complété le: {loadedInstance.CompletedAt}");
        Console.WriteLine($"Durée totale: {loadedInstance.TotalDuration?.TotalSeconds:F2} secondes");
    }

    // Résumé par type de nœud
    var summary = loadedInstance.ExecutionHistory
        .GroupBy(h => h.NodeType)
        .Select(g => new
        {
            Type = g.Key,
            Count = g.Count(),
            AvgDuration = g.Average(h => h.Duration.TotalMilliseconds),
            TotalDuration = g.Sum(h => h.Duration.TotalMilliseconds)
        });

    Console.WriteLine("\n=== Résumé par type de nœud ===");
    foreach (var item in summary)
    {
        Console.WriteLine($"{item.Type}: {item.Count} exécutions");
        Console.WriteLine($"  Durée moyenne: {item.AvgDuration:F2} ms");
        Console.WriteLine($"  Durée totale: {item.TotalDuration:F2} ms");
    }
}

// Exemple d'implémentation d'un exécuteur
public class SampleExecutor : IBpmMediator
{
    public Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"Executing Command: {commandName}");
        return Task.CompletedTask;
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"Executing Decision: {decisionName}");
        return Task.FromResult("approved");
    }
}
