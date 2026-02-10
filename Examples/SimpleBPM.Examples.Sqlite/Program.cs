using Microsoft.Data.Sqlite;
using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.Definition;
using SimpleBPM.Examples.Sqlite;
using SimpleBPM.Handlers;

// ============================================================
// Configuration SQLite
// ============================================================
var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "simplebpm_example.db");
var config = new SqliteConfiguration(dbPath, "BPM");

Console.WriteLine($"SQLite database: {dbPath}");
Console.WriteLine();

using var connection = new SqliteConnection(config.ConnectionString);
connection.Open();

// Enable WAL mode and foreign keys for better performance
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
    cmd.ExecuteNonQuery();
}

// ============================================================
// Initialize repository and create tables
// ============================================================
var repository = new SqliteProcessRepository(config, connection);
await repository.InitializeDatabaseAsync();
Console.WriteLine("Database initialized successfully.");
Console.WriteLine();

// ============================================================
// Define the process using the fluent builder
// ============================================================
var processDefinition = ProcessBuilder.Create("OrderProcess")
    .Business("ValidateOrder", "Validate the order")
    .Business("CheckInventory", "Check inventory levels")
    .Decision("DecideApproval", "Approval decision", routes => routes
        .When("approved", "ProcessApproved")
        .When("rejected", "ProcessRejected"))
    .Business("ProcessApproved", "Process approved order")
        .Then("WaitPayment")
    .Business("ProcessRejected", "Process rejected order")
    .WaitForSignal("WaitPayment", "Wait for payment confirmation")
    .Interactive("ManualReview", "Manual review step")
    .Build();

Console.WriteLine($"Process defined: {processDefinition.Name}");
Console.WriteLine($"Number of nodes: {processDefinition.Nodes.Count}");
Console.WriteLine();

// ============================================================
// Configure handlers
// ============================================================
var executor = new SampleExecutor();
var handlers = new INodeHandler[]
{
    new BusinessNodeHandler(executor),
    new DecisionNodeHandler(executor)
};

// Create the engine with the SQLite repository
var engine = new FlowEngine(new[] { processDefinition }, repository, handlers);

// ============================================================
// Generate a process ID using the repository's sequence
// ============================================================
var processId = await repository.ObtenirSequenceAsync("SEQ_PROCESSUS");

// Start a new process instance
var instance = new ProcessInstance(processId, "order-42");
instance = await engine.ExecuteAsync(instance);

Console.WriteLine("=== After initial execution ===");
Console.WriteLine($"Process ID: {instance.ProcessId}");
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Current Node: {instance.CurrentNodeId}");
Console.WriteLine($"Steps executed: {instance.ExecutionHistory.Count}");
Console.WriteLine();

// ============================================================
// Display execution history
// ============================================================
Console.WriteLine("=== Execution History ===");
foreach (var history in instance.ExecutionHistory)
{
    Console.WriteLine($"  Node: {history.NodeName} ({history.NodeType})");
    Console.WriteLine($"    Started:  {history.StartedAt:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"    Finished: {history.CompletedAt:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"    Duration: {history.Duration.TotalMilliseconds:F2} ms");
    Console.WriteLine($"    Success:  {history.Success}");
    if (!string.IsNullOrEmpty(history.NextNodeId))
        Console.WriteLine($"    Next:     {history.NextNodeId}");
    Console.WriteLine();
}

// ============================================================
// Send signal to resume the process
// ============================================================
Console.WriteLine("=== Sending 'PaymentReceived' signal ===");
instance = await engine.SignalAsync(instance, "PaymentReceived");
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Current Node: {instance.CurrentNodeId}");
Console.WriteLine($"Steps executed: {instance.ExecutionHistory.Count}");
Console.WriteLine();

// ============================================================
// Continue after interactive step
// ============================================================
Console.WriteLine("=== Continuing after manual review ===");
instance = await engine.ContinueAsync(instance);
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Steps executed: {instance.ExecutionHistory.Count}");
if (instance.CompletedAt.HasValue)
    Console.WriteLine($"Total duration: {instance.TotalDuration?.TotalMilliseconds:F2} ms");
Console.WriteLine();

// ============================================================
// Load the process back from SQLite
// ============================================================
Console.WriteLine("=== Reloading from SQLite ===");
var loaded = await engine.LoadProcessAsync(processId);
if (loaded != null)
{
    Console.WriteLine($"Process ID: {loaded.ProcessId}");
    Console.WriteLine($"Aggregate: {loaded.AggregateId}");
    Console.WriteLine($"Status: {loaded.Status}");
    Console.WriteLine($"Started: {loaded.StartedAt:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"History entries: {loaded.ExecutionHistory.Count}");

    // Summary by node type
    Console.WriteLine();
    Console.WriteLine("=== Summary by node type ===");
    var summary = loaded.ExecutionHistory
        .GroupBy(h => h.NodeType)
        .Select(g => new
        {
            Type = g.Key,
            Count = g.Count(),
            AvgDuration = g.Average(h => h.Duration.TotalMilliseconds),
            TotalDuration = g.Sum(h => h.Duration.TotalMilliseconds)
        });

    foreach (var item in summary)
    {
        Console.WriteLine($"  {item.Type}: {item.Count} execution(s), avg {item.AvgDuration:F2} ms, total {item.TotalDuration:F2} ms");
    }
}

Console.WriteLine();
Console.WriteLine("Example complete. Database file persisted at:");
Console.WriteLine($"  {dbPath}");

// ============================================================
// Sample ICommandExecutor implementation
// ============================================================
public class SampleExecutor : ICommandExecutor
{
    public Task ExecuteCommandAsync(string commandName, long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [Execute] {commandName} (process={processId}, aggregate={aggregateId})");
        return Task.CompletedTask;
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [Decision] {decisionName} -> approved");
        return Task.FromResult("approved");
    }
}
