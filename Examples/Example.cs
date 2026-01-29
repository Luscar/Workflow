using SimpleBPM;
using SimpleBPM.Nodes;
using SimpleBPM.Handlers;

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
var executor = new SampleExecutor();
var handlers = new INodeHandler[]
{
    new BusinessNodeHandler(executor),
    new DecisionNodeHandler(executor)
};

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
var engine = new ProcessEngine(processDefinition, handlers: handlers);
var instance = new ProcessInstance("order-123", "aggregate-456");

// Première exécution - s'arrêtera au premier nœud d'attente/interactif
instance = await engine.ExecuteAsync(instance);
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeId}");

// Simuler la réception d'un signal
instance = await engine.SignalAsync(instance, "PaymentReceived");
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeId}");

// Continuer après interaction utilisateur
instance = await engine.ContinueAsync(instance);
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeId}");
