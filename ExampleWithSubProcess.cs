using SimpleBPM;
using SimpleBPM.Nodes;

ProcessEngine.ConfigureExecutor(new SampleExecutor());

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

// Nœud de sous-processus
var validationSubProcessNode = new SubProcessNode(subProcessDefinition)
{
    Name = "Validation complète",
    InheritAggregateId = true  // Le sous-processus hérite de l'ID d'agrégat
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
var engine = new ProcessEngine(mainProcessDefinition);

var context = new ProcessContext("order-456", "aggregate-789");

// Ajouter des données d'entrée pour le sous-processus
context.Data["SUB_INPUT_OrderAmount"] = 1500.00;
context.Data["SUB_INPUT_CustomerType"] = "Premium";

Console.WriteLine("=== Démarrage du processus principal ===");
context = await engine.ExecuteAsync(context);

Console.WriteLine($"Statut: {context.Status}");
Console.WriteLine($"Nœud courant: {context.CurrentNodeId}");
Console.WriteLine($"Nombre d'étapes exécutées: {context.ExecutionHistory.Count}");

// Afficher l'historique
Console.WriteLine("\n=== Historique d'exécution ===");
foreach (var history in context.ExecutionHistory)
{
    Console.WriteLine($"- {history.NodeName} ({history.NodeType}): {history.Duration.TotalMilliseconds:F0}ms - Succès: {history.Success}");
}

// Si le processus est en attente (à cause de l'approbation manuelle dans le sous-processus)
if (context.Status == ProcessStatus.WaitingInteraction)
{
    Console.WriteLine("\n=== Le processus attend une interaction (approbation manuelle) ===");
    Console.WriteLine("Simulation de l'approbation...");
    
    // Continuer le processus
    await Task.Delay(1000);
    context = await engine.ContinueAsync(context);
    
    Console.WriteLine($"Nouveau statut: {context.Status}");
    Console.WriteLine($"Nombre total d'étapes: {context.ExecutionHistory.Count}");
}

// Afficher les données de sortie du sous-processus
Console.WriteLine("\n=== Données de sortie ===");
foreach (var kvp in context.Data)
{
    if (kvp.Key.StartsWith("SUB_OUTPUT_"))
    {
        Console.WriteLine($"{kvp.Key}: {kvp.Value}");
    }
}

Console.WriteLine("\n=== Résumé final ===");
Console.WriteLine($"Statut final: {context.Status}");
if (context.CompletedAt.HasValue)
{
    Console.WriteLine($"Durée totale: {context.TotalDuration?.TotalSeconds:F2} secondes");
}

public class SampleExecutor : ICommandQueryExecutor
{
    public async Task ExecuteAsync(string commandOrQueryName, string processId, string? aggregateId, bool isQuery)
    {
        await Task.Delay(Random.Shared.Next(50, 150));
        Console.WriteLine($"  Exécution: {commandOrQueryName}");
    }

    public async Task<string> ExecuteDecisionAsync(string queryName, string processId, string? aggregateId)
    {
        await Task.Delay(Random.Shared.Next(50, 100));
        Console.WriteLine($"  Décision: {queryName}");
        return "approved";
    }
}
