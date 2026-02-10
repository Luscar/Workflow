using Microsoft.Extensions.DependencyInjection;
using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.ExampleClient;
using SimpleBPM.Handlers;
using SimpleBPM.Localisation;

// ============================================================
// SimpleBPM Example Client - Loan Approval Workflow
// ============================================================
//
// This project demonstrates how to use SimpleBPM as a client:
//   1. Implement ICommandExecutor for business logic
//   2. Implement IGestionTache for task management (optional)
//   3. Define processes using the fluent ProcessBuilder API
//   4. Register everything via DI with AddSimpleBPM()
//
// The loan approval workflow includes:
//   - Business nodes (validate, credit check, disburse)
//   - A decision node (approve/reject based on credit)
//   - An interactive node (underwriter manual review)
//   - A signal node (wait for document signing)
//   - A subprocess (identity/income/employment verification)
// ============================================================

Console.WriteLine("=== SimpleBPM Example Client: Loan Approval Workflow ===");
Console.WriteLine();

// --- Dependency Injection Setup ---
var services = new ServiceCollection();

// 1. Register client implementations
services.AddSingleton<ICommandExecutor, LoanCommandExecutor>();
services.AddSingleton<IGestionTache, LoanTaskManager>();

// 2. Register process definitions
services.AddSingleton(LoanProcessDefinitions.CreateLoanApprovalProcess());

// 3. Register SimpleBPM services (handlers, engine, etc.)
//    - Use AddSimpleBPM() for in-memory (no persistence)
//    - Use AddSimpleBPM("PREFIX") for Oracle persistence
services.AddSimpleBPM();

var provider = services.BuildServiceProvider();

// --- Resolve from DI ---
// Handlers and definitions are wired automatically by the container.
// The client never needs to know about BusinessNodeHandler, DecisionNodeHandler, etc.
var handlers = provider.GetServices<INodeHandler>();
var loanProcess = provider.GetRequiredService<ProcessDefinition>();

Console.WriteLine($"Process: {loanProcess.Name} v{loanProcess.Version}");
Console.WriteLine($"Nodes:   {loanProcess.Nodes.Count}");
Console.WriteLine($"Start:   {loanProcess.StartNodeId}");
Console.WriteLine();

// --- Create and execute a process instance ---
var engine = new FlowEngine(new[] { loanProcess }, handlers: handlers);

var instance = new ProcessInstance(1001, "LOAN-2024-001");
instance.Variables["ApplicantName"] = "Jane Doe";
instance.Variables["ApplicantId"] = "APP-12345";
instance.Variables["LoanAmount"] = 50000.00;
instance.Variables["LoanTerm"] = 36;

Console.WriteLine("--- Starting loan approval workflow ---");
Console.WriteLine();
instance = await engine.ExecuteAsync(instance);

Console.WriteLine();
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Current node: {instance.CurrentNodeId}");
Console.WriteLine($"Steps completed: {instance.CompletedStepsCount}");
Console.WriteLine();

// --- Handle the interactive node (underwriter review) ---
if (instance.Status == ProcessStatus.WaitingInteraction)
{
    Console.WriteLine("--- Simulating underwriter review completion ---");
    Console.WriteLine();

    // In a real app, a user would complete this via a UI
    instance.Variables["UnderwriterDecision"] = "approved";
    instance.Variables["ReviewNotes"] = "All documents verified, applicant meets criteria.";

    instance = await engine.ContinueAsync(instance);

    Console.WriteLine();
    Console.WriteLine($"Status: {instance.Status}");
    Console.WriteLine($"Current node: {instance.CurrentNodeId}");
    Console.WriteLine();
}

// --- Handle the signal node (document signing) ---
if (instance.Status == ProcessStatus.WaitingSignal)
{
    Console.WriteLine("--- Simulating document signing signal ---");
    Console.WriteLine();

    instance = await engine.SignalAsync(instance, "WaitDocumentSigning");

    Console.WriteLine();
    Console.WriteLine($"Status: {instance.Status}");
    Console.WriteLine($"Current node: {instance.CurrentNodeId ?? "(none)"}");
    Console.WriteLine();
}

// --- Print execution summary ---
Console.WriteLine("=== Execution Summary ===");
Console.WriteLine();
Console.WriteLine($"Process ID:     {instance.ProcessId}");
Console.WriteLine($"Aggregate ID:   {instance.AggregateId}");
Console.WriteLine($"Final status:   {instance.Status}");
Console.WriteLine($"Steps executed: {instance.ExecutionHistory.Count}");
Console.WriteLine($"Successful:     {instance.CompletedStepsCount}");
Console.WriteLine($"Failed:         {instance.FailedStepsCount}");

if (instance.CompletedAt.HasValue)
{
    Console.WriteLine($"Total duration: {instance.TotalDuration?.TotalMilliseconds:F0} ms");
}

Console.WriteLine();
Console.WriteLine("--- Step-by-step history ---");
foreach (var history in instance.ExecutionHistory)
{
    var status = history.Success ? "OK" : "FAIL";
    Console.WriteLine($"  {history.NodeName,-25} [{history.NodeType,-12}] {status}  ({history.Duration.TotalMilliseconds:F0} ms)");
    if (!string.IsNullOrEmpty(history.ErrorMessage))
        Console.WriteLine($"    Error: {history.ErrorMessage}");
}

Console.WriteLine();
Console.WriteLine("--- Process variables ---");
foreach (var variable in instance.Variables.OrderBy(v => v.Key))
{
    Console.WriteLine($"  {variable.Key,-25} = {variable.Value}");
}

Console.WriteLine();
Console.WriteLine("=== Done ===");
