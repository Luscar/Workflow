# SimpleBPM — Client Integration Guide

This guide walks you through integrating SimpleBPM into a client application from scratch.
It covers defining processes with the fluent builder, implementing business logic handlers,
wiring up dependency injection, and operating processes at runtime.

---

## Table of contents

1. [Prerequisites](#1-prerequisites)
2. [Package reference](#2-package-reference)
3. [Defining a process — the Fluent Builder](#3-defining-a-process--the-fluent-builder)
   - [Builder basics](#31-builder-basics)
   - [BusinessNode](#32-businessnode)
   - [DecisionNode](#33-decisionnode)
   - [InteractiveNode](#34-interactivenode)
   - [WaitForSignalNode](#35-waitforsignalnode)
   - [WaitUntilDateNode](#36-waituntildatenode)
   - [SubProcessNode](#37-subprocessnode)
   - [EndNode](#38-endnode)
   - [Node parameters](#39-node-parameters)
   - [OnEnter command](#310-onenter-command)
   - [Manual chaining with Then and Break](#311-manual-chaining-with-then-and-break)
4. [Defining a process — JSON](#4-defining-a-process--json)
5. [Implementing business logic](#5-implementing-business-logic)
   - [ICommandHandler](#51-icommandhandler)
   - [IQueryHandler](#52-iqueryhandler)
   - [IBpmMediateur (direct approach)](#53-ibpmmediateur-direct-approach)
   - [IGestionTache (optional task management)](#54-igestiontache-optional-task-management)
6. [Dependency injection setup](#6-dependency-injection-setup)
   - [Microsoft DI — unified builder](#61-microsoft-di--unified-builder)
   - [Microsoft DI — step-by-step](#62-microsoft-di--step-by-step)
   - [Autofac module](#63-autofac-module)
   - [Oracle persistence](#64-oracle-persistence)
7. [Operating processes at runtime](#7-operating-processes-at-runtime)
   - [Creating a process instance](#71-creating-a-process-instance)
   - [Completing an interactive step](#72-completing-an-interactive-step)
   - [Sending a signal](#73-sending-a-signal)
   - [Querying process state](#74-querying-process-state)
   - [Searching by variable](#75-searching-by-variable)
8. [Monitoring](#8-monitoring)
9. [Version migration](#9-version-migration)
10. [Complete example — loan approval workflow](#10-complete-example--loan-approval-workflow)

---

## 1. Prerequisites

- .NET 8 or later
- Oracle database (or use the built-in in-memory store for testing)
- A reference to the `SimpleBPM` project/package

---

## 2. Package reference

```xml
<ProjectReference Include="../SimpleBPM/SimpleBPM.csproj" />
```

For Oracle persistence you also need:

```xml
<PackageReference Include="Dapper" Version="2.*" />
<PackageReference Include="Oracle.ManagedDataAccess.Core" Version="23.*" />
```

---

## 3. Defining a process — the Fluent Builder

`ProcessBuilder` is the recommended way to define processes. It gives you compile-time validation, IntelliSense, and automatic node chaining.

### 3.1 Builder basics

```csharp
using SimpleBPM;
using SimpleBPM.Definition;

ProcessDefinition process = ProcessBuilder.Create("MyProcess", "1.0")
    .Business("Step1", "First step")
    .Business("Step2", "Second step")
    .Build();
```

**Automatic chaining rule**: Every node is automatically linked to the previous node, *unless*:
- The previous node is a `DecisionNode` (it manages its own routes), or
- The previous node already has an explicit successor set via `.Then()`, or
- A `.Break()` was called to sever the chain.

`Build()` validates that every `NextNodeId` and every decision route references a node that actually exists, throwing `InvalidOperationException` if not.

---

### 3.2 BusinessNode

A `BusinessNode` executes a named command by calling `IBpmMediateur.ExecuteCommandAsync`.

```csharp
ProcessBuilder.Create("OrderProcess")
    .Business("ValidateOrder", "Validate the order")
    .Business("SendConfirmation", "Send confirmation email")
    .Build();
```

The string passed as the first argument is *both* the node name *and* the command name dispatched to `ICommandHandler`.

If the command throws an exception, the node marks the process as `Failed` with the exception message.

---

### 3.3 DecisionNode

A `DecisionNode` routes the process to different branches. It supports two modes.

#### Mode A — Variable conditions (recommended)

Conditions are evaluated in declaration order. The first matching condition determines the next node. If none match, the default node is used.

```csharp
ProcessBuilder.Create("LoanProcess")
    .Business("CheckCredit", "Check credit score")
    .Decision("CreditDecision", "Credit routing", routes => routes
        .When("approved", "CalculateTerms")
        .When("rejected",  "RejectApplication"))
    .Business("CalculateTerms", "Calculate loan terms")
        .Then("Disburse")
    .Business("RejectApplication", "Send rejection letter")
        .End("Rejected", "Application rejected")
    .Business("Disburse", "Disburse funds")
    .Build();
```

The `.When(condition, targetNodeId)` calls on `DecisionRouteBuilder` map a *result string* to a *node name*.
The result string is returned by `IQueryHandler.HandleAsync`.

#### Mode B — Inline variable conditions

Instead of a query handler, you can evaluate process variables directly — no extra handler class needed:

```csharp
var decisionNode = new DecisionNode
{
    Name = "CheckAmount",
    DisplayName = "Check order amount"
};

decisionNode
    .AddCondition("Amount", 10_000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "HighValueRoute")
    .AddCondition("Amount", 1_000,  OperateurFiltre.Superieur,       TypeDonnee.Nombre, "StandardRoute")
    .SetNoeudParDefaut("StandardRoute");
```

You can mix inline conditions into a fluent builder by calling `.Build()` on an existing `ProcessDefinition` and adding the node manually, but for most cases Mode A (query handler) is cleaner inside the builder.

**Available operators** (`OperateurFiltre`):

| Operator | Meaning | Applicable types |
|---|---|---|
| `Egal` | Equals | All |
| `Different` | Not equal | All |
| `Superieur` | Strictly greater | `Nombre`, `Date`, `Texte` |
| `SuperieurOuEgal` | Greater or equal | `Nombre`, `Date`, `Texte` |
| `Inferieur` | Strictly less | `Nombre`, `Date`, `Texte` |
| `InferieurOuEgal` | Less or equal | `Nombre`, `Date`, `Texte` |
| `Contient` | Contains substring | `Texte` |
| `CommencePar` | Starts with | `Texte` |
| `FinitPar` | Ends with | `Texte` |

**Available data types** (`TypeDonnee`): `Texte`, `Nombre`, `Date`, `Booleen`

---

### 3.4 InteractiveNode

An `InteractiveNode` pauses the process and waits for a human to call `TerminerEtapeAsync` or `TerminerEtapeEnCoursAsync`.

```csharp
ProcessBuilder.Create("ApprovalProcess")
    .Business("Prepare", "Prepare documents")
    .Interactive("ManagerApproval", "Manager approval")
    .Business("Archive", "Archive approved documents")
    .Build();
```

When the engine reaches this node:
1. `ProcessStatus` is set to `WaitingInteraction`.
2. `CurrentNodeId` is recorded so the engine knows where to resume.
3. If `IGestionTache` is registered, `CreerTacheAsync` is called (creates a task visible to users).
4. If an `OnEnterCommand` is configured (see §3.10), it is executed before stopping.

When the user completes the step, call `TerminerEtapeEnCoursAsync` (or `TerminerEtapeAsync`).
The engine calls `OnLeaveAsync` → `FermerTacheAsync` (if `IGestionTache` is registered), then continues from the next node.

---

### 3.5 WaitForSignalNode

A `WaitForSignalNode` pauses the process until an external system sends a named signal.

```csharp
ProcessBuilder.Create("OrderProcess")
    .Business("PlaceOrder", "Place order")
    .WaitForSignal("WaitPayment", "Waiting for payment confirmation")
    .Business("FulfillOrder", "Fulfill order")
    .Build();
```

The node name is also used as the expected signal name. Send the signal via:

```csharp
await flowService.EnvoyerSignalAsync(processId, "WaitPayment");
```

The engine only resumes if the received signal name matches `WaitForSignalNode.SignalName`.

---

### 3.6 WaitUntilDateNode

A `WaitUntilDateNode` pauses the process until a specific date/time. Three date resolution strategies are available:

#### Fixed date

```csharp
.WaitUntilDate("WaitDeadline", new DateTime(2025, 12, 31), "Wait until year-end")
```

#### Date read from a process variable

```csharp
// The process must have a variable named "ScheduledDate" containing a DateTime
.WaitUntilDate("WaitScheduled", "ScheduledDate", "Wait until scheduled date")
```

#### Dynamically computed date (lambda)

```csharp
.WaitUntilDate("WaitCooldown",
    instance => instance.StartedAt.AddDays(7),
    "Wait 7 days after start")
```

#### Date returned by a query handler

```csharp
.WaitUntilDateQuery("WaitDynamic", "GetProcessingDate",
    queryParameters: new() { ["Type"] = "express" },
    displayName: "Wait for processing date")
```

The scheduler responsible for resuming date-paused instances is typically implemented by the client (e.g. a background job that calls `TerminerEtapeEnCoursAsync` when the date is reached).

---

### 3.7 SubProcessNode

A `SubProcessNode` delegates execution to a nested process definition. Variables flow between parent and child via explicit mappings.

#### Using an existing ProcessDefinition

```csharp
var verificationProcess = ProcessBuilder.Create("Verification")
    .Business("VerifyId",     "Verify identity")
    .Business("VerifyIncome", "Verify income")
    .Build();

ProcessBuilder.Create("LoanProcess")
    .Business("Start", "Start application")
    .SubProcess("Verification", verificationProcess,
        inputMapping:  new() { ["ApplicantId"] = "ClientId" },
        outputMapping: new() { ["VerificationResult"] = "IsVerified" },
        inheritAggregateId: true,
        displayName: "Applicant Verification")
    .Business("Continue", "Continue after verification")
    .Build();
```

#### Using an inline builder

```csharp
ProcessBuilder.Create("LoanProcess")
    .Business("Start", "Start application")
    .SubProcess("Verification", sub => sub
        .Business("VerifyId",     "Verify identity")
        .Business("VerifyIncome", "Verify income"),
        inputMapping:  new() { ["ApplicantId"] = "ClientId" },
        outputMapping: new() { ["VerificationResult"] = "IsVerified" })
    .Business("Continue", "Continue after verification")
    .Build();
```

**Mapping rules**:

- `inputMapping`  — `{ "ParentVar": "SubVar" }` copies `ParentVar` from the parent into `SubVar` in the child before execution.
- `outputMapping` — `{ "SubVar": "ParentVar" }` copies `SubVar` from the child back into `ParentVar` in the parent after completion.

If the sub-process itself reaches a waiting node, the parent is also paused with `RequiresStop = true`. On the next call to `ContinueAsync` on the parent, the engine detects the existing child instance and resumes it.

---

### 3.8 EndNode

An `EndNode` explicitly terminates a branch. The engine marks the instance as `Completed` and stops.

```csharp
ProcessBuilder.Create("OrderProcess")
    .Business("ProcessApproved", "Handle approved order")
        .Then("Deliver")
    .Business("ProcessRejected", "Handle rejected order")
        .End("OrderRejected", "Order was rejected")   // branch terminates here
    .Business("Deliver", "Deliver the order")
    .Build();
```

After `.End(...)`, the automatic chain is broken — the next node added by the builder starts a fresh, unconnected segment. This is how you define multiple terminal branches in a single builder call.

You do *not* need an `EndNode` for the natural end of a linear process. When `FlowEngine` reaches a node with no `NextNodeIds` and the node completes without `RequiresStop`, it sets `Status = Completed` automatically.

---

### 3.9 Node parameters

Every node can carry a static `Dictionary<string, object>` of parameters. These are passed verbatim to `IBpmMediateur` (and therefore to `ICommandHandler` / `IQueryHandler`) at execution time.

```csharp
ProcessBuilder.Create("NotificationProcess")
    .Business("SendEmail", "Send confirmation email")
        .WithParameter("Template",  "OrderConfirmation")
        .WithParameter("Priority",  "High")
    .Business("Archive", "Archive document")
        .WithParameters(new()
        {
            ["RetentionDays"] = 90,
            ["Compress"]      = true
        })
    .Build();
```

`WithParameter` and `WithParameters` operate on the *most recently added* node. Calling them before any node has been added throws `InvalidOperationException`.

Receiving the parameters in a handler:

```csharp
public class SendEmailHandler : ICommandHandler
{
    public string CommandName => "SendEmail";

    public Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var template = parameters?["Template"]?.ToString() ?? "Default";
        var priority = parameters?["Priority"]?.ToString() ?? "Normal";
        // ... send email logic
        return Task.CompletedTask;
    }
}
```

---

### 3.10 OnEnter command

Blocking nodes (`Interactive`, `WaitForSignal`, `WaitUntilDate`) can execute a command *just before* pausing. This is useful for sending notifications or logging.

```csharp
ProcessBuilder.Create("ApprovalProcess")
    .Business("Prepare", "Prepare documents")
    .Interactive("ManagerApproval", "Manager approval")
        .WithOnEnterCommand("NotifyManager")
        .WithOnEnterCommandParameter("Channel", "email")
    .Business("Archive", "Archive")
    .Build();
```

When the engine reaches `ManagerApproval`:
1. `NotifyManager` is dispatched via `IBpmMediateur.ExecuteCommandAsync` with `{ "Channel": "email" }`.
2. The engine then pauses with `WaitingInteraction`.

If the `OnEnter` command throws, the node fails (status `Failed`).

---

### 3.11 Manual chaining with Then and Break

By default the builder links each new node to the previous one. Use `.Then()` and `.Break()` for fine-grained control.

```csharp
ProcessBuilder.Create("FlexibleProcess")
    .Business("NodeA", "A")
        .Then("NodeC")          // explicit link: A → C (auto-chain to B is suppressed)
    .Business("NodeB", "B")     // NodeB is added but NOT auto-linked from A
        .Then("NodeD")
    .Business("NodeC", "C")     // referenced by NodeA
    .Business("NodeD", "D")     // referenced by NodeB
    .Build();
```

`.Break()` sets the current node pointer to `null`, so the next node added has no automatic predecessor:

```csharp
ProcessBuilder.Create("ParallelBranches")
    .Business("Root", "Root node")
        .Then("BranchA")
        .Then("BranchB")
    .Break()
    .Business("BranchA", "Branch A")
    .Break()
    .Business("BranchB", "Branch B")
    .Build();
```

---

## 4. Defining a process — JSON

JSON definitions are useful for external configuration, dynamic loading, or tooling integration.

```json
{
  "name": "OrderProcess",
  "version": "1.0",
  "startNode": "ValidateOrder",
  "nodes": [
    {
      "name": "ValidateOrder",
      "type": "Business",
      "command": "ValidateOrder",
      "parameters": { "StrictMode": true },
      "next": ["CheckInventory"]
    },
    {
      "name": "CheckInventory",
      "type": "Business",
      "command": "CheckInventory",
      "next": ["RouteDecision"]
    },
    {
      "name": "RouteDecision",
      "type": "Decision",
      "query": "RouteDecision",
      "routes": {
        "in_stock": "ProcessOrder",
        "out_of_stock": "BackOrder"
      }
    },
    {
      "name": "ProcessOrder",
      "type": "Business",
      "command": "ProcessOrder",
      "next": ["WaitPayment"]
    },
    {
      "name": "WaitPayment",
      "type": "WaitForSignal",
      "signal": "PaymentReceived"
    },
    {
      "name": "BackOrder",
      "type": "Interactive",
      "next": ["ProcessOrder"]
    },
    {
      "name": "OrderRejected",
      "type": "End"
    }
  ]
}
```

Loading:

```csharp
using SimpleBPM.Definition;

// From a JSON string
ProcessDefinition process = ProcessJsonLoader.FromJson(json);

// From a file
ProcessDefinition process = ProcessJsonLoader.FromJsonFile("processes/order.json");

// Export to JSON
string json = ProcessJsonLoader.ToJson(process);
```

---

## 5. Implementing business logic

### 5.1 ICommandHandler

Implement one `ICommandHandler` per business command. The `CommandName` property must match the node name (or the command name passed to `.Business(...)`) in the process definition.

```csharp
using SimpleBPM.Abstractions;

public class ValidateOrderHandler : ICommandHandler
{
    private readonly IOrderRepository _orders;

    public ValidateOrderHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public string CommandName => "ValidateOrder";

    public async Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var strictMode = parameters?["StrictMode"] is true;
        var order = await _orders.GetByProcessIdAsync(processId);
        order.Validate(strictMode);
        await _orders.SaveAsync(order);
    }
}
```

Key rules:
- Constructors are resolved by the DI container — inject whatever you need.
- Throwing any exception marks the node (and the process) as `Failed`.
- The `processId` identifies the running workflow; `aggregateId` is the optional domain aggregate ID you passed at creation time.

### 5.2 IQueryHandler

`IQueryHandler` is used for `DecisionNode` external queries. The handler must return a string that matches one of the routes defined on the decision node.

```csharp
public class CreditDecisionHandler : IQueryHandler
{
    private readonly ICreditService _credit;

    public CreditDecisionHandler(ICreditService credit)
    {
        _credit = credit;
    }

    public string QueryName => "CreditDecision";

    public async Task<string> HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var score = await _credit.GetScoreAsync(aggregateId);
        return score >= 650 ? "approved" : "rejected";
    }
}
```

If the returned string does not match any defined route, the engine marks the process `Failed` with a descriptive message.

### 5.3 IBpmMediateur (direct approach)

If you prefer a single centralised mediator class instead of individual handlers, implement `IBpmMediateur` directly. This is mutually exclusive with `AddCommandHandlers` — choose one approach.

```csharp
public class MyBpmMediateur : IBpmMediateur
{
    public async Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        switch (commandName)
        {
            case "ValidateOrder":   await ValidateOrderAsync(processId, parameters); break;
            case "CheckInventory":  await CheckInventoryAsync(processId); break;
            default:
                throw new InvalidOperationException($"Unknown command: {commandName}");
        }
    }

    public async Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        return decisionName switch
        {
            "RouteDecision" => await EvaluateRouteAsync(processId),
            _ => throw new InvalidOperationException($"Unknown decision: {decisionName}")
        };
    }

    // ... private methods
}
```

Register it:

```csharp
services.AddSingleton<IBpmMediateur, MyBpmMediateur>();
```

### 5.4 IGestionTache (optional task management)

If your application has a task inbox or work queue, implement `IGestionTache` to automatically create and close tasks when `InteractiveNode`s are entered and exited.

```csharp
public class MyGestionTache : IGestionTache
{
    private readonly ITaskRepository _tasks;

    public MyGestionTache(ITaskRepository tasks)
    {
        _tasks = tasks;
    }

    public async Task CreerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
    {
        await _tasks.CreateAsync(new WorkTask
        {
            ProcessId      = processId,
            AggregateId    = aggregateId,
            ProcessName    = definitionName,
            TaskName       = nodeName,
            AssignedAt     = DateTime.UtcNow
        });
    }

    public async Task FermerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
    {
        await _tasks.CloseAsync(processId, nodeName);
    }
}
```

The lifecycle is automatic:
1. Engine enters `InteractiveNode` → `CreerTacheAsync` is called.
2. Client calls `TerminerEtapeEnCoursAsync` → `FermerTacheAsync` is called before execution resumes.

---

## 6. Dependency injection setup

### 6.1 Microsoft DI — unified builder (recommended)

```csharp
using System.Reflection;
using SimpleBPM.Localisation;

// Program.cs
services.AddSimpleBPM(options =>
{
    // Auto-discover all ICommandHandler and IQueryHandler in this assembly
    options.ScanHandlers(Assembly.GetExecutingAssembly());

    // Optional: register a task manager
    options.UseTaskManager<MyGestionTache>();

    // Oracle persistence (omit to use in-memory)
    options.UseOracle("ORD");   // prefix: 3-10 uppercase letters

    // Register process definitions
    options.AddProcess(OrderProcessDefinitions.CreateOrderProcess());
    options.AddProcess(OrderProcessDefinitions.CreateRefundProcess());
});
```

Then register your Oracle connection:

```csharp
services.AddScoped<IDbConnection>(sp =>
{
    var conn = new OracleConnection(configuration.GetConnectionString("Oracle"));
    conn.Open();
    return conn;
});
```

### 6.2 Microsoft DI — step-by-step

For finer control:

```csharp
// 1. Register individual command / query handlers (auto-discovers all ICommandHandler / IQueryHandler)
services.AddCommandHandlers(Assembly.GetExecutingAssembly());

// 2. Optional services
services.AddSingleton<IGestionTache, MyGestionTache>();

// 3. Oracle connection (managed by the client)
services.AddScoped<IDbConnection>(sp =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return conn;
});

// 4. Process definitions (each as a singleton)
services.AddSingleton(OrderProcessDefinitions.CreateOrderProcess());
services.AddSingleton(OrderProcessDefinitions.CreateRefundProcess());

// 5. SimpleBPM core (Oracle backend)
services.AddSimpleBPM(tablePrefix: "ORD");
// or in-memory:
// services.AddSimpleBPM();
```

### 6.3 Autofac module

```csharp
using Autofac;
using SimpleBPM.Localisation;

var builder = new ContainerBuilder();

builder.RegisterModule(new SimpleBPMAutofacModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<MyGestionTache>();
    module.UseOracle("ORD");
    module.AddProcess(OrderProcessDefinitions.CreateOrderProcess());
}));

// Oracle connection — register separately
builder.Register(ctx =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return (IDbConnection)conn;
}).InstancePerLifetimeScope();

var container = builder.Build();
```

### 6.4 Oracle persistence

The `OracleProcessRepository` requires an open `IDbConnection` (injected by scope). It uses Dapper for all queries.

**Table prefix rules**:
- Between 3 and 10 characters.
- Letters only (no digits or special characters).
- Automatically uppercased.

**Tables created**:

| Table | Description |
|---|---|
| `{PREFIX}_PROCESS_CONTEXT` | One row per process instance — state, variables (JSON), timestamps |
| `{PREFIX}_HISTORIQUE_EXECUTION_NOEUD` | One row per node execution — audit trail with durations |

Initialize the schema once per environment (e.g. during application startup):

```csharp
// Resolve from DI and call once
var repo = serviceProvider.GetRequiredService<IProcessRepository>();
if (repo is OracleProcessRepository oracleRepo)
    await oracleRepo.InitializeDatabaseAsync();
```

Or apply `schema.sql` manually for environments where the app should not auto-create tables.

---

## 7. Operating processes at runtime

All runtime interaction goes through `IFlowService`.

```csharp
public interface IFlowService
{
    Task<long>           CreateProcessInstanceAsync(string definitionName, Dictionary<string, object>? variables = null);
    Task<Processus>      ObtenirAsync(long instanceProcessId);
    Task                 TerminerEtapeAsync(long idInstanceNoeud, object contenu);
    Task                 TerminerEtapeEnCoursAsync(long idInstanceProcessus, Dictionary<string, object>? contenu = null);
    Task                 EnvoyerSignalAsync(long idInstanceProcessus, string signalName);
    Task<IEnumerable<string>> ObtenirSignauxEnAttenteAsync(long idInstanceProcessus);
    Task<InstanceNode>   ObtenirNoeudAsync(long idInstanceNoeud);
    Task<List<Processus>> RechercherParVariableAsync(List<FiltreVariable> filtres);
    Task<List<Processus>> ObtenirEnfantsAsync(long idInstanceParent);
    Task<MigrationResult> MigrateAsync(long processId, ProcessDefinition targetDefinition, ProcessMigration migration);
}
```

### 7.1 Creating a process instance

```csharp
// Inject IFlowService via DI
private readonly IFlowService _flowService;

long processId = await _flowService.CreateProcessInstanceAsync("OrderProcess", new()
{
    ["OrderId"]     = "ORD-001",
    ["OrderAmount"] = 2500.00,
    ["CustomerId"]  = "CUST-42"
});
```

`CreateProcessInstanceAsync` starts execution immediately and runs nodes until a waiting node is reached or the process completes.
The returned `long` is the unique process ID (from an Oracle sequence or the in-memory counter).

### 7.2 Completing an interactive step

Two methods are available:

**By process ID** (most common):

```csharp
await _flowService.TerminerEtapeEnCoursAsync(processId, new()
{
    ["ManagerDecision"] = "approved",
    ["Comment"]         = "All checks passed."
});
```

**By node instance ID** (when you have stored the specific node record):

```csharp
await _flowService.TerminerEtapeAsync(nodeInstanceId, new Dictionary<string, object>
{
    ["ManagerDecision"] = "approved"
});
```

Both methods:
1. Merge the provided dictionary into `instance.Variables`.
2. Call `OnLeaveAsync` on the current node handler (closes the task if `IGestionTache` is registered).
3. Resume execution from the next node.

### 7.3 Sending a signal

```csharp
await _flowService.EnvoyerSignalAsync(processId, "PaymentReceived");
```

If the process is in `WaitingSignal` and the signal name matches the expected signal, execution resumes immediately.

To discover which signals a process is waiting for:

```csharp
IEnumerable<string> signals = await _flowService.ObtenirSignauxEnAttenteAsync(processId);
// e.g. ["PaymentReceived"]
```

### 7.4 Querying process state

```csharp
Processus process = await _flowService.ObtenirAsync(processId);

Console.WriteLine(process.Status);         // Running, WaitingInteraction, Completed, ...
Console.WriteLine(process.CurrentNodeId);  // Name of the current node
Console.WriteLine(process.AggregateId);    // Domain aggregate link
```

`Processus` is a read-only DTO — it does not expose internal engine state.

```csharp
public class Processus
{
    public long           Id                { get; }
    public long?          AggregateId       { get; }
    public string?        DefinitionName    { get; }
    public string?        DefinitionVersion { get; }
    public ProcessStatus  Status            { get; }
    public string?        CurrentNodeId     { get; }
    public DateTime       StartedAt         { get; }
    public DateTime?      CompletedAt       { get; }
    public TimeSpan?      TotalDuration     { get; }
    public string?        ErrorMessage      { get; }
    public Dictionary<string, object> Variables { get; }
}
```

### 7.5 Searching by variable

Use `RechercherParVariableAsync` to find process instances by their variable values. All filters are combined with AND logic.

```csharp
using SimpleBPM;

// Single condition (equality)
var results = await _flowService.RechercherParVariableAsync(new()
{
    new FiltreVariable("CustomerId", "CUST-42", OperateurFiltre.Egal, TypeDonnee.Texte)
});

// Multiple conditions
var results = await _flowService.RechercherParVariableAsync(new()
{
    new FiltreVariable("OrderAmount",    1_000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre),
    new FiltreVariable("OrderStatus",  "Open",  OperateurFiltre.Egal,            TypeDonnee.Texte),
    new FiltreVariable("CreatedAt",    DateTime.Today.AddDays(-7), OperateurFiltre.Superieur, TypeDonnee.Date)
});
```

The same operators and data types from `DecisionNode` apply here.

---

## 8. Monitoring

For dashboards, admin panels, or reporting services that need read-only access to process state, use `IProcessMonitor` instead of `IFlowService`.

```csharp
// Register (Microsoft DI)
services.AddProcessMonitoring();   // no engine, no IBpmMediateur needed

// or in an Autofac dashboard module:
builder.RegisterModule(new ProcessMonitoringAutofacModule());
```

```csharp
public interface IProcessMonitor
{
    List<ProcessDefinition>           GetDefinitions();
    Task<List<Processus>>             GetAllInstancesAsync();
    Task<List<Processus>>             GetRootInstancesAsync();
    Task<List<Processus>>             GetAllDescendantsAsync(long processId);
    Task<List<Processus>>             GetInstancesByStatusAsync(ProcessStatus status);
    Task<Processus>                   GetInstanceAsync(long processId);
    Task<List<NodeInstance>>          GetExecutionHistoryAsync(long processId);
    Task<Dictionary<ProcessStatus, int>> GetStatusSummaryAsync();
}
```

Example — dashboard showing all waiting processes:

```csharp
var waiting = await _monitor.GetInstancesByStatusAsync(ProcessStatus.WaitingInteraction);
foreach (var p in waiting)
{
    Console.WriteLine($"{p.DefinitionName} #{p.Id} — waiting at: {p.CurrentNodeId}");
}

// Status counts for a summary widget
var summary = await _monitor.GetStatusSummaryAsync();
// { Running: 5, WaitingInteraction: 12, Completed: 348, Failed: 2 }
```

---

## 9. Version migration

When you release a new version of a process definition, instances that are currently paused (waiting) can be migrated without data loss.

Only instances in `WaitingInteraction`, `WaitingSignal`, or `WaitingDate` can be migrated — running instances cannot be interrupted.

### Define a migration

```csharp
var migration = new ProcessMigration("1.0", "2.0")
    .MapNode("Review", "DetailedReview")           // renamed node
    .SetVariable("MigratedFromV1", true)           // add new variable
    .RenameVariable("OldStatus", "ReviewStatus")   // rename variable
    .RemoveVariable("DeprecatedFlag");              // remove obsolete variable
```

Or load from JSON:

```json
{
  "fromVersion": "1.0",
  "toVersion":   "2.0",
  "nodeMappings": {
    "Review": "DetailedReview"
  },
  "variableTransforms": [
    { "type": "set",    "name": "MigratedFromV1",  "value": true },
    { "type": "rename", "name": "OldStatus",        "newName": "ReviewStatus" },
    { "type": "remove", "name": "DeprecatedFlag" }
  ]
}
```

```csharp
var migration = ProcessMigrationLoader.FromJsonFile("migrations/v1_to_v2.json");
```

### Apply the migration

```csharp
var v2Definition = OrderProcessDefinitions.CreateOrderProcessV2();

MigrationResult result = await _flowService.MigrateAsync(processId, v2Definition, migration);

if (result.Success)
    Console.WriteLine($"Migrated from {result.PreviousVersion} to {result.NewVersion}");
else
    Console.WriteLine($"Migration failed: {result.ErrorMessage}");
```

**Variable transformation types**:

| Type | Effect |
|---|---|
| `set` | Creates or overwrites a variable with the given value |
| `rename` | Renames a variable key (value preserved) |
| `remove` | Deletes a variable |

---

## 10. Complete example — loan approval workflow

This section shows a realistic end-to-end integration based on the `SimpleBPM.ExampleClient` project.

### Process definition

```csharp
using SimpleBPM;
using SimpleBPM.Definition;

public static class LoanProcessDefinitions
{
    public static ProcessDefinition CreateLoanApprovalProcess()
    {
        var verificationSub = ProcessBuilder.Create("VerificationProcess", "1.0")
            .Business("VerifyIdentity",   "Verify identity documents")
            .Business("VerifyIncome",     "Verify income statements")
            .Business("VerifyEmployment", "Verify employment status")
            .Build();

        return ProcessBuilder.Create("LoanApproval", "1.0")
            // Validate the application and attach required documents list
            .Business("ValidateApplication", "Validate Loan Application")
                .WithParameter("RequiredDocuments", "ID,Income,Employment")

            // Run a sub-process for applicant verification with variable mapping
            .SubProcess("Verification", verificationSub,
                inputMapping:  new() { ["ApplicantId"] = "ApplicantId" },
                outputMapping: new() { ["VerificationPassed"] = "IsVerified" },
                displayName: "Applicant Verification")

            // Check credit score
            .Business("CheckCredit", "Check Credit Score")

            // Route based on credit decision (returned by CreditDecisionHandler)
            .Decision("CreditDecision", "Credit Decision", routes => routes
                .When("approved", "CalculateTerms")
                .When("rejected",  "RejectLoan"))

            // Approved branch
            .Business("CalculateTerms", "Calculate Loan Terms")
                .Then("ManualReview")       // skip auto-chain past the rejected branch
            .Business("RejectLoan", "Reject Loan Application")
                .End("LoanRejected", "Loan Application Rejected")  // branch terminates here

            // Interactive step — pause and wait for underwriter
            .Interactive("ManualReview", "Underwriter Review")

            // Wait for the applicant to sign documents
            .WaitForSignal("WaitDocumentSigning", "Wait for Document Signing")

            // Disburse and close
            .Business("DisburseFunds", "Disburse Loan Funds")
                .End("LoanApproved", "Loan Approved and Disbursed")

            .Build();
    }
}
```

### Command handlers

```csharp
// ValidateApplicationHandler.cs
public class ValidateApplicationHandler : ICommandHandler
{
    public string CommandName => "ValidateApplication";

    public Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var required = parameters?["RequiredDocuments"]?.ToString() ?? "";
        Console.WriteLine($"[ValidateApplication] Required docs: {required}");
        return Task.CompletedTask;
    }
}

// DisburseFundsHandler.cs
public class DisburseFundsHandler : ICommandHandler
{
    private readonly IPaymentGateway _gateway;

    public DisburseFundsHandler(IPaymentGateway gateway) => _gateway = gateway;

    public string CommandName => "DisburseFunds";

    public async Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        await _gateway.DisburseAsync(processId);
    }
}
```

### Query handler

```csharp
// CreditDecisionHandler.cs
public class CreditDecisionHandler : IQueryHandler
{
    private readonly ICreditBureau _bureau;

    public CreditDecisionHandler(ICreditBureau bureau) => _bureau = bureau;

    public string QueryName => "CreditDecision";

    public async Task<string> HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        int score = await _bureau.GetScoreAsync(aggregateId);
        return score >= 650 ? "approved" : "rejected";
    }
}
```

### Task manager

```csharp
public class LoanTaskManager : IGestionTache
{
    private readonly ITaskStore _store;

    public LoanTaskManager(ITaskStore store) => _store = store;

    public Task CreerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
        => _store.CreateAsync(processId, nodeName, "Underwriter");

    public Task FermerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
        => _store.CloseAsync(processId, nodeName);
}
```

### DI setup (Autofac)

```csharp
var containerBuilder = new ContainerBuilder();

containerBuilder.RegisterModule(new SimpleBPMAutofacModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<LoanTaskManager>();
    module.AddProcess(LoanProcessDefinitions.CreateLoanApprovalProcess());
    // module.UseOracle("LN");   // uncomment for Oracle
}));

containerBuilder.RegisterType<FakeCreditBureau>().As<ICreditBureau>().SingleInstance();
containerBuilder.RegisterType<FakePaymentGateway>().As<IPaymentGateway>().SingleInstance();

var container = containerBuilder.Build();
```

### Runtime usage

```csharp
await using var scope = container.BeginLifetimeScope();
var flowService = scope.Resolve<IFlowService>();

// 1. Start the workflow
long processId = await flowService.CreateProcessInstanceAsync("LoanApproval", new()
{
    ["ApplicantId"] = "APP-12345",
    ["LoanAmount"]  = 50_000.00,
    ["LoanTerm"]    = 36
});

var process = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Status: {process.Status}");   // WaitingInteraction

// 2. Underwriter completes the interactive review
await flowService.TerminerEtapeEnCoursAsync(processId, new()
{
    ["UnderwriterDecision"] = "approved",
    ["ReviewNotes"]         = "All documents verified."
});

process = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Status: {process.Status}");   // WaitingSignal

// 3. Applicant signs documents — external system sends signal
var pending = await flowService.ObtenirSignauxEnAttenteAsync(processId);
// pending == ["WaitDocumentSigning"]

await flowService.EnvoyerSignalAsync(processId, "WaitDocumentSigning");

process = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Status: {process.Status}");   // Completed
```

---

## Quick reference

| Task | Method |
|---|---|
| Start a process | `IFlowService.CreateProcessInstanceAsync` |
| Complete interactive step | `IFlowService.TerminerEtapeEnCoursAsync` |
| Complete step by node ID | `IFlowService.TerminerEtapeAsync` |
| Send signal | `IFlowService.EnvoyerSignalAsync` |
| Get process state | `IFlowService.ObtenirAsync` |
| Get pending signals | `IFlowService.ObtenirSignauxEnAttenteAsync` |
| Search by variable | `IFlowService.RechercherParVariableAsync` |
| Get child processes | `IFlowService.ObtenirEnfantsAsync` |
| Migrate to new version | `IFlowService.MigrateAsync` |
| Monitor (read-only) | `IProcessMonitor.*` |
