using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;
using SimpleBPM.Handlers;

var executor = new SampleExecutor();
var handlers = new INodeHandler[]
{
    new BusinessNodeHandler(executor),
    new DecisionNodeHandler(executor)
};

// ========================================
// 1. Définir un SOUS-PROCESSUS de validation
// ========================================
var subProcessDefinition = new ProcessDefinition("ValidationSubProcess");

var validateDataNode = new BusinessNode("ValidateData") { Name = "Valider les données" };
var checkRulesNode = new BusinessNode("CheckBusinessRules") { Name = "Vérifier les règles" };
var approveNode = new InteractiveNode() { Name = "Approbation manuelle" };

validateDataNode.NextNodeIds.Add(checkRulesNode.Id);
checkRulesNode.NextNodeIds.Add(approveNode.Id);

subProcessDefinition
    .AddNode(validateDataNode)
    .AddNode(checkRulesNode)
    .AddNode(approveNode);

// ========================================
// 2. Définir le PROCESSUS PRINCIPAL
// ========================================
var mainProcessDefinition = new ProcessDefinition("OrderProcessWithSubProcess");

var startNode = new BusinessNode("StartOrder") { Name = "Démarrer la commande" };

// Nœud de sous-processus avec mapping explicite
var validationSubProcessNode = new SubProcessNode(subProcessDefinition)
{
    Name = "Validation complète",
    InheritAggregateId = true,
    InputMapping = new()
    {
        ["OrderAmount"] = "Amount",
        ["CustomerType"] = "ClientType"
    },
    OutputMapping = new()
    {
        ["ValidationResult"] = "IsValid"
    }
};

var processPaymentNode = new BusinessNode("ProcessPayment") { Name = "Traiter le paiement" };
var completeNode = new BusinessNode("CompleteOrder") { Name = "Compléter la commande" };

startNode.NextNodeIds.Add(validationSubProcessNode.Id);
validationSubProcessNode.NextNodeIds.Add(processPaymentNode.Id);
processPaymentNode.NextNodeIds.Add(completeNode.Id);

mainProcessDefinition
    .AddNode(startNode)
    .AddNode(validationSubProcessNode)
    .AddNode(processPaymentNode)
    .AddNode(completeNode);

// ========================================
// 3. Exécuter le processus principal
// ========================================
var engine = new FlowEngine(new[] { mainProcessDefinition }, handlers: handlers);

var instance = new ProcessInstance(456, "aggregate-789");

// Ajouter des variables d'entrée (noms normaux, le mapping se charge du transfert)
instance.Variables["OrderAmount"] = 1500.00;
instance.Variables["CustomerType"] = "Premium";

Console.WriteLine("=== Démarrage du processus principal ===");
instance = await engine.ExecuteAsync(instance);

Console.WriteLine($"Statut: {instance.Status}");
Console.WriteLine($"Nœud courant: {instance.CurrentNodeId}");
Console.WriteLine($"Nombre d'étapes exécutées: {instance.ExecutionHistory.Count}");

// Afficher l'historique
Console.WriteLine("\n=== Historique d'exécution ===");
foreach (var history in instance.ExecutionHistory)
{
    Console.WriteLine($"- {history.NodeName} ({history.NodeType}): {history.Duration.TotalMilliseconds:F0}ms - Succès: {history.Success}");
}

// Si le processus est en attente (à cause de l'approbation manuelle dans le sous-processus)
if (instance.Status == ProcessStatus.WaitingInteraction)
{
    Console.WriteLine("\n=== Le processus attend une interaction (approbation manuelle) ===");
    Console.WriteLine("Simulation de l'approbation...");

    // Continuer le processus
    await Task.Delay(1000);
    instance = await engine.ContinueAsync(instance);

    Console.WriteLine($"Nouveau statut: {instance.Status}");
    Console.WriteLine($"Nombre total d'étapes: {instance.ExecutionHistory.Count}");
}

// Afficher les variables de sortie récupérées via le mapping
Console.WriteLine("\n=== Variables de sortie ===");
if (instance.Variables.TryGetValue("IsValid", out var isValid))
{
    Console.WriteLine($"IsValid: {isValid}");
}

Console.WriteLine("\n=== Résumé final ===");
Console.WriteLine($"Statut final: {instance.Status}");
if (instance.CompletedAt.HasValue)
{
    Console.WriteLine($"Durée totale: {instance.TotalDuration?.TotalSeconds:F2} secondes");
}

public class SampleExecutor : ICommandExecutor
{
    public async Task ExecuteCommandAsync(string commandName, long processId, string? aggregateId)
    {
        await Task.Delay(Random.Shared.Next(50, 150));
        Console.WriteLine($"  Exécution: {commandName}");
    }

    public async Task<string> EvaluateDecisionAsync(string decisionName, long processId, string? aggregateId)
    {
        await Task.Delay(Random.Shared.Next(50, 100));
        Console.WriteLine($"  Décision: {decisionName}");
        return "approved";
    }
}
