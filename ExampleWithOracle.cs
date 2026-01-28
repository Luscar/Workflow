using SimpleBPM;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;

// Configuration Oracle avec préfixe de table
var oracleConfig = new OracleConfiguration(
    connectionString: "User Id=myuser;Password=mypass;Data Source=localhost:1521/XEPDB1",
    tablePrefix: "ABC" // Préfixe de 3 à 10 lettres
);

// Créer la factory de connexion
var connexionFactory = new OracleConnexionBDFactory(oracleConfig.ConnectionString);

// Créer le repository avec la factory
var repository = new OracleProcessRepository(oracleConfig, connexionFactory);

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
var context = new ProcessContext("order-123", "aggregate-456");
context = await engine.ExecuteAsync(context);
Console.WriteLine($"Status: {context.Status}, Current Node: {context.CurrentNodeId}");
// Le contexte est automatiquement sauvegardé dans Oracle avec le préfixe ABC_PROCESS_CONTEXT

// Plus tard, charger le processus depuis la base de données
var loadedContext = await engine.LoadProcessAsync("order-123");
if (loadedContext != null)
{
    Console.WriteLine($"Loaded process - Status: {loadedContext.Status}");
    
    // Envoyer un signal
    loadedContext = await engine.SignalAsync(loadedContext, "PaymentReceived");
    Console.WriteLine($"After signal - Status: {loadedContext.Status}");
    
    // Continuer l'exécution
    loadedContext = await engine.ContinueAsync(loadedContext);
    Console.WriteLine($"Final - Status: {loadedContext.Status}");
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
