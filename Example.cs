using SimpleBPM;
using SimpleBPM.Nodes;

// Exemple d'implémentation d'un exécuteur
public class SampleExecutor : ICommandQueryExecutor
{
    public Task ExecuteAsync(string commandOrQueryName, string processId, string? aggregateId, bool isQuery)
    {
        Console.WriteLine($"Executing {(isQuery ? "Query" : "Command")}: {commandOrQueryName} for Process: {processId}, Aggregate: {aggregateId}");
        return Task.CompletedTask;
    }

    public Task<string> ExecuteDecisionAsync(string queryName, string processId, string? aggregateId)
    {
        Console.WriteLine($"Executing Decision Query: {queryName} for Process: {processId}, Aggregate: {aggregateId}");
        // Retourne une condition (ex: "approved", "rejected", etc.)
        return Task.FromResult("approved");
    }
}

// Configuration
ProcessEngine.ConfigureExecutor(new SampleExecutor());

// Définition d'un processus simple
var processDefinition = new ProcessDefinition("OrderProcess");

var validateOrderNode = new BusinessNode("ValidateOrder", isQuery: false) { Name = "Validate Order" };
var checkInventoryNode = new BusinessNode("CheckInventory", isQuery: true) { Name = "Check Inventory" };
var decisionNode = new DecisionNode("DecideApproval") { Name = "Approval Decision" };
var approvedNode = new BusinessNode("ProcessApprovedOrder") { Name = "Process Approved" };
var rejectedNode = new BusinessNode("ProcessRejectedOrder") { Name = "Process Rejected" };
var waitNode = new WaitForSignalNode("PaymentReceived") { Name = "Wait Payment" };
var interactiveNode = new InteractiveNode() { Name = "Manual Review" };

// Construction du flux
validateOrderNode.NextNodeIds.Add(checkInventoryNode.Id);
checkInventoryNode.NextNodeIds.Add(decisionNode.Id);

decisionNode
    .AddRoute("approved", approvedNode.Id)
    .AddRoute("rejected", rejectedNode.Id);

approvedNode.NextNodeIds.Add(waitNode.Id);
waitNode.NextNodeIds.Add(interactiveNode.Id);

// Ajout des nœuds au processus
processDefinition
    .AddNode(validateOrderNode)
    .AddNode(checkInventoryNode)
    .AddNode(decisionNode)
    .AddNode(approvedNode)
    .AddNode(rejectedNode)
    .AddNode(waitNode)
    .AddNode(interactiveNode);

// Exécution
var engine = new ProcessEngine(processDefinition);
var context = new ProcessContext("order-123", "aggregate-456");

// Première exécution - s'arrêtera au premier nœud d'attente/interactif
context = await engine.ExecuteAsync(context);
Console.WriteLine($"Status: {context.Status}, Current Node: {context.CurrentNodeId}");

// Simuler la réception d'un signal
context = await engine.SignalAsync(context, "PaymentReceived");
Console.WriteLine($"Status: {context.Status}, Current Node: {context.CurrentNodeId}");

// Continuer après interaction utilisateur
context = await engine.ContinueAsync(context);
Console.WriteLine($"Status: {context.Status}, Current Node: {context.CurrentNodeId}");
