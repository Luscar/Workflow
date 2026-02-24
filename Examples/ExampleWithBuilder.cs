using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.Definition;
using SimpleBPM.Handlers;

var executor = new SampleExecutor();
var handlers = new INodeHandler[]
{
    new BusinessNodeHandler(executor),
    new DecisionNodeHandler(executor)
};

// ============================================================
// MÉTHODE 1: Fluent Builder
// ============================================================
Console.WriteLine("=== Définition avec Fluent Builder ===\n");

var processFromBuilder = ProcessBuilder.Create("OrderProcess")
    .Business("ValidateOrder", "Valider la commande")
    .Business("CheckInventory", "Vérifier le stock")
    .Decision("DecideApproval", "Décision d'approbation", routes => routes
        .When("approved", "ProcessApproved")
        .When("rejected", "ProcessRejected"))
    .Business("ProcessApproved", "Traiter commande approuvée")
        .Then("WaitPayment")
    .Business("ProcessRejected", "Traiter commande rejetée")
    .WaitForSignal("WaitPayment", "Attente paiement")
    .Interactive("ManualReview", "Revue manuelle")
    .Build();

Console.WriteLine($"Processus créé: {processFromBuilder.Name}");
Console.WriteLine($"Nombre de nœuds: {processFromBuilder.Nodes.Count}");
Console.WriteLine();

// ============================================================
// MÉTHODE 2: JSON
// ============================================================
Console.WriteLine("=== Définition avec JSON ===\n");

var json = """
{
    "name": "OrderProcess",
    "startNode": "ValidateOrder",
    "nodes": [
        {
            "name": "ValidateOrder",
            "type": "Business",
            "displayName": "Valider la commande",
            "command": "ValidateOrder",
            "next": ["CheckInventory"]
        },
        {
            "name": "CheckInventory",
            "type": "Business",
            "displayName": "Vérifier le stock",
            "command": "CheckInventory",
            "next": ["DecideApproval"]
        },
        {
            "name": "DecideApproval",
            "type": "Decision",
            "displayName": "Décision d'approbation",
            "query": "DecideApproval",
            "routes": {
                "approved": "ProcessApproved",
                "rejected": "ProcessRejected"
            }
        },
        {
            "name": "ProcessApproved",
            "type": "Business",
            "displayName": "Traiter commande approuvée",
            "command": "ProcessApproved",
            "next": ["WaitPayment"]
        },
        {
            "name": "ProcessRejected",
            "type": "Business",
            "displayName": "Traiter commande rejetée",
            "command": "ProcessRejected"
        },
        {
            "name": "WaitPayment",
            "type": "WaitForSignal",
            "displayName": "Attente paiement",
            "signal": "PaymentReceived",
            "next": ["ManualReview"]
        },
        {
            "name": "ManualReview",
            "type": "Interactive",
            "displayName": "Revue manuelle"
        }
    ]
}
""";

var processFromJson = ProcessJsonLoader.FromJson(json);

Console.WriteLine($"Processus créé: {processFromJson.Name}");
Console.WriteLine($"Nombre de nœuds: {processFromJson.Nodes.Count}");
Console.WriteLine();

// ============================================================
// Exporter un processus en JSON
// ============================================================
Console.WriteLine("=== Export en JSON ===\n");

var exportedJson = ProcessJsonLoader.ToJson(processFromBuilder);
Console.WriteLine(exportedJson);
Console.WriteLine();

// ============================================================
// Exécuter le processus
// ============================================================
Console.WriteLine("=== Exécution du processus ===\n");

var engine = new FlowEngine(new[] { processFromBuilder }, handlers: handlers);
var instance = new ProcessInstance(789, "aggregate-123");

instance = await engine.ExecuteAsync(instance);

Console.WriteLine($"Statut: {instance.Status}");
Console.WriteLine($"Nœud courant: {instance.CurrentNodeName}");
Console.WriteLine($"Étapes exécutées: {instance.ExecutionHistory.Count}");

foreach (var history in instance.ExecutionHistory)
{
    Console.WriteLine($"  - {history.NodeName}: {(history.Success ? "OK" : "ERREUR")}");
}

// ============================================================
// Exemple avec sous-processus inline
// ============================================================
Console.WriteLine("\n=== Processus avec sous-processus inline ===\n");

var processWithSubProcess = ProcessBuilder.Create("MainProcess")
    .Business("Start", "Démarrage")
    .SubProcess("Validation", sub => sub
        .Business("ValidateData", "Valider données")
        .Business("CheckRules", "Vérifier règles")
        .Interactive("Approve", "Approbation"),
        inputMapping: new() { ["OrderId"] = "Id" },
        outputMapping: new() { ["Result"] = "ValidationResult" })
    .Business("Complete", "Fin")
    .Build();

Console.WriteLine($"Processus principal: {processWithSubProcess.Name}");
Console.WriteLine($"Nombre de nœuds: {processWithSubProcess.Nodes.Count}");

public class SampleExecutor : ICommandExecutor
{
    public Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"    Exécution: {commandName}");
        return Task.CompletedTask;
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"    Décision: {decisionName} -> approved");
        return Task.FromResult("approved");
    }
}
