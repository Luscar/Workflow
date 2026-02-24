using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;
using SimpleBPM.Handlers;

// Exemple d'implémentation d'un exécuteur
public class SampleExecutor : IBpmMediator
{
    public Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"Executing Command: {commandName} for Process: {processId}, Aggregate: {aggregateId}");
        return Task.CompletedTask;
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"Executing Decision: {decisionName} for Process: {processId}, Aggregate: {aggregateId}");
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

var validateOrderNode = new BusinessNode("ValidateOrder") { Name = "ValidateOrder", DisplayName = "Validate Order" };
var checkInventoryNode = new BusinessNode("CheckInventory") { Name = "CheckInventory", DisplayName = "Check Inventory" };
var decisionNode = new DecisionNode("DecideApproval") { Name = "DecideApproval", DisplayName = "Approval Decision" };
var approvedNode = new BusinessNode("ProcessApprovedOrder") { Name = "ProcessApproved", DisplayName = "Process Approved" };
var rejectedNode = new BusinessNode("ProcessRejectedOrder") { Name = "ProcessRejected", DisplayName = "Process Rejected" };
var waitNode = new WaitForSignalNode("PaymentReceived") { Name = "WaitPayment", DisplayName = "Wait Payment" };
var interactiveNode = new InteractiveNode() { Name = "ManualReview", DisplayName = "Manual Review" };

// Construction du flux
validateOrderNode.NextNodeIds.Add(checkInventoryNode.Name);
checkInventoryNode.NextNodeIds.Add(decisionNode.Name);

decisionNode
    .AddRoute("approved", approvedNode.Name)
    .AddRoute("rejected", rejectedNode.Name);

approvedNode.NextNodeIds.Add(waitNode.Name);
waitNode.NextNodeIds.Add(interactiveNode.Name);

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
var engine = new FlowEngine(new[] { processDefinition }, handlers: handlers);
var instance = new ProcessInstance(123, "aggregate-456");

// Première exécution - s'arrêtera au premier nœud d'attente/interactif
instance = await engine.ExecuteAsync(instance);
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeName}");

// Simuler la réception d'un signal
instance = await engine.SignalAsync(instance, "PaymentReceived");
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeName}");

// Continuer après interaction utilisateur
instance = await engine.ContinueAsync(instance);
Console.WriteLine($"Status: {instance.Status}, Current Node: {instance.CurrentNodeName}");
