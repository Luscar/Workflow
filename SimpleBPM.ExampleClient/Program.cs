using System.Reflection;
using Autofac;
using SimpleBPM;
using SimpleBPM.ExampleClient;
using SimpleBPM.Localisation;

// ============================================================
// SimpleBPM Example Client - Loan Approval Workflow
// ============================================================
//
// This project demonstrates how to use SimpleBPM as a client:
//   1. Implement IBpmCommandHandler / IBpmQueryHandler for business logic
//   2. Implement IGestionTache for task management (optional)
//   3. Define processes using the fluent ProcessBuilder API
//   4. Register everything via a single RegistrationBpmModule
//   5. Interact exclusively through IFlowService
//
// The client never references handlers or the engine directly.
// ============================================================

Console.WriteLine("=== SimpleBPM Example Client: Loan Approval Workflow ===");
Console.WriteLine();

// --- Dependency Injection Setup (Autofac) ---
var containerBuilder = new ContainerBuilder();

containerBuilder.RegisterModule(new RegistrationBpmModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<LoanTaskManager>();
    module.AddProcess(LoanProcessDefinitions.CreateLoanApprovalProcess());
}));

var container = containerBuilder.Build();
await using var scope = container.BeginLifetimeScope();
var flowService = scope.Resolve<IFlowService>();

// --- Start a loan approval process ---
Console.WriteLine("--- Starting loan approval workflow ---");
Console.WriteLine();

var processId = await flowService.CreateProcessInstanceAsync("LoanApproval", new Dictionary<string, object>
{
    ["ApplicantName"] = "Jane Doe",
    ["ApplicantId"] = "APP-12345",
    ["LoanAmount"] = 50000.00,
    ["LoanTerm"] = 36
});

var process = await flowService.ObtenirAsync(processId);
Console.WriteLine();
Console.WriteLine($"Process ID: {process.Id}");
Console.WriteLine($"Status:     {process.Status}");
Console.WriteLine($"Node:       {process.CurrentNodeId}");
Console.WriteLine();

// --- Complete the interactive step (underwriter review) ---
if (process.Status == ProcessStatus.WaitingInteraction)
{
    Console.WriteLine("--- Simulating underwriter review completion ---");
    Console.WriteLine();

    await flowService.TerminerEtapeEnCoursAsync(processId, new Dictionary<string, object>
    {
        ["UnderwriterDecision"] = "approved",
        ["ReviewNotes"] = "All documents verified, applicant meets criteria."
    });

    process = await flowService.ObtenirAsync(processId);
    Console.WriteLine();
    Console.WriteLine($"Status: {process.Status}");
    Console.WriteLine($"Node:   {process.CurrentNodeId}");
    Console.WriteLine();
}

// --- Send the document signing signal ---
if (process.Status == ProcessStatus.WaitingSignal)
{
    var pendingSignals = await flowService.ObtenirSignauxEnAttenteAsync(processId);
    Console.WriteLine($"Pending signals: {string.Join(", ", pendingSignals)}");
    Console.WriteLine();
    Console.WriteLine("--- Simulating document signing signal ---");
    Console.WriteLine();

    await flowService.EnvoyerSignalAsync(processId, "WaitDocumentSigning");

    process = await flowService.ObtenirAsync(processId);
    Console.WriteLine();
    Console.WriteLine($"Status: {process.Status}");
    Console.WriteLine($"Node:   {process.CurrentNodeId ?? "(none)"}");
    Console.WriteLine();
}

// --- Print final state ---
Console.WriteLine("=== Final State ===");
Console.WriteLine();
Console.WriteLine($"Process ID:   {process.Id}");
Console.WriteLine($"Aggregate:    {process.AggregateId}");
Console.WriteLine($"Status:       {process.Status}");

Console.WriteLine();
Console.WriteLine("--- Variables ---");
foreach (var variable in process.Variables.OrderBy(v => v.Key))
{
    Console.WriteLine($"  {variable.Key,-25} = {variable.Value}");
}

Console.WriteLine();
Console.WriteLine("=== Done ===");
