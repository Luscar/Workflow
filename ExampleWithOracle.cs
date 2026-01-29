using Oracle.ManagedDataAccess.Client;
using SimpleBPM;
using SimpleBPM.Nodes;
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

// Configuration de l'exécuteur
ProcessEngine.ConfigureExecutor(new SampleExecutor());

// Définition du processus
var processDefinition = new ProcessDefinition("OrderProcess");

var validateOrderNode = new BusinessNode("ValidateOrder") { Name = "Validate Order" };
var checkInventoryNode = new BusinessNode("CheckInventory", isQuery: true) { Name = "Check Inventory" };
var decisionNode = new DecisionNode("DecideApproval") { Name = "Approval Decision" };
var approvedNode = new BusinessNode("ProcessApprovedOrder") { Name = "Process Approved" };
var rejectedNode = new BusinessNode("ProcessRejectedOrder") { Name = "Process Rejected" };
var waitNode = new WaitForSignalNode("PaymentReceived") { Name = "Wait Payment" };
var interactiveNode = new InteractiveNode() { Name = "Manual Review" };

validateOrderNode.NextNodeIds.Add(checkInventoryNode.Id);
checkInventoryNode.NextNodeIds.Add(decisionNode.Id);

decisionNode
    .AddRoute("approved", approvedNode.Id)
    .AddRoute("rejected", rejectedNode.Id);

approvedNode.NextNodeIds.Add(waitNode.Id);
waitNode.NextNodeIds.Add(interactiveNode.Id);

processDefinition
    .AddNode(validateOrderNode)
    .AddNode(checkInventoryNode)
    .AddNode(decisionNode)
    .AddNode(approvedNode)
    .AddNode(rejectedNode)
    .AddNode(waitNode)
    .AddNode(interactiveNode);

// Créer le moteur avec le repository
var engine = new ProcessEngine(processDefinition, repository);

// Démarrer un nouveau processus
var instance = new ProcessInstance("order-123", "aggregate-456");
instance = await engine.ExecuteAsync(instance);
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeId}");
// L'instance est automatiquement sauvegardée dans Oracle avec le préfixe ABC_PROCESS_CONTEXT

// Plus tard, charger le processus depuis la base de données
var loadedInstance = await engine.LoadProcessAsync("order-123");
if (loadedInstance != null)
{
    Console.WriteLine($"Loaded process - Status: {loadedInstance.Status}");

    // Envoyer un signal
    loadedInstance = await engine.SignalAsync(loadedInstance, "PaymentReceived");
    Console.WriteLine($"After signal - Status: {loadedInstance.Status}");

    // Continuer l'exécution
    loadedInstance = await engine.ContinueAsync(loadedInstance);
    Console.WriteLine($"Final - Status: {loadedInstance.Status}");
}

// Exemple d'implémentation d'un exécuteur
public class SampleExecutor : ICommandQueryExecutor
{
    public Task ExecuteAsync(string commandOrQueryName, string processId, string? aggregateId, bool isQuery)
    {
        Console.WriteLine($"Executing {(isQuery ? "Query" : "Command")}: {commandOrQueryName}");
        return Task.CompletedTask;
    }

    public Task<string> ExecuteDecisionAsync(string queryName, string processId, string? aggregateId)
    {
        Console.WriteLine($"Executing Decision Query: {queryName}");
        return Task.FromResult("approved");
    }
}
