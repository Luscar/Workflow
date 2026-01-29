using Oracle.ManagedDataAccess.Client;
using SimpleBPM;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;

// Configuration et initialisation
var oracleConfig = new OracleConfiguration(
    connectionString: "User Id=myuser;Password=mypass;Data Source=localhost:1521/XEPDB1",
    tablePrefix: "ABC"
);

using var connection = new OracleConnection(oracleConfig.ConnectionString);
connection.Open();

var repository = new OracleProcessRepository(oracleConfig, connection);
await repository.InitializeDatabaseAsync();

ProcessEngine.ConfigureExecutor(new SampleExecutor());

// Définition du processus
var processDefinition = new ProcessDefinition("OrderProcess");

var validateOrderNode = new BusinessNode("ValidateOrder") { Name = "Validate Order" };
var checkInventoryNode = new BusinessNode("CheckInventory", isQuery: true) { Name = "Check Inventory" };
var decisionNode = new DecisionNode("DecideApproval") { Name = "Approval Decision" };
var approvedNode = new BusinessNode("ProcessApprovedOrder") { Name = "Process Approved" };
var waitNode = new WaitForSignalNode("PaymentReceived") { Name = "Wait Payment" };

validateOrderNode.NextNodeIds.Add(checkInventoryNode.Id);
checkInventoryNode.NextNodeIds.Add(decisionNode.Id);
decisionNode.AddRoute("approved", approvedNode.Id);
approvedNode.NextNodeIds.Add(waitNode.Id);

processDefinition
    .AddNode(validateOrderNode)
    .AddNode(checkInventoryNode)
    .AddNode(decisionNode)
    .AddNode(approvedNode)
    .AddNode(waitNode);

var engine = new ProcessEngine(processDefinition, repository);

// Exécuter le processus
var instance = new ProcessInstance("order-123", "aggregate-456");
instance = await engine.ExecuteAsync(instance);

Console.WriteLine("=== État du processus ===");
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Durée actuelle: {instance.CurrentDuration}");
Console.WriteLine($"Étapes complétées: {instance.CompletedStepsCount}");
Console.WriteLine($"Étapes échouées: {instance.FailedStepsCount}");

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

// Simuler l'attente et continuer
await Task.Delay(1000);
instance = await engine.SignalAsync(instance, "PaymentReceived");

Console.WriteLine("\n=== Après signal ===");
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Nouvelles étapes exécutées: {instance.ExecutionHistory.Count}");

// Charger depuis la base de données plus tard
var loadedInstance = await engine.LoadProcessAsync("order-123");
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

    Console.WriteLine($"\nNombre total d'étapes: {loadedInstance.ExecutionHistory.Count}");

    // Afficher un résumé par type de nœud
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

public class SampleExecutor : ICommandQueryExecutor
{
    public async Task ExecuteAsync(string commandOrQueryName, string processId, string? aggregateId, bool isQuery)
    {
        // Simuler un délai de traitement
        await Task.Delay(Random.Shared.Next(50, 200));
        Console.WriteLine($"Executing {(isQuery ? "Query" : "Command")}: {commandOrQueryName}");
    }

    public async Task<string> ExecuteDecisionAsync(string queryName, string processId, string? aggregateId)
    {
        await Task.Delay(Random.Shared.Next(50, 150));
        Console.WriteLine($"Executing Decision Query: {queryName}");
        return "approved";
    }
}
