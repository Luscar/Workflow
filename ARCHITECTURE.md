# SimpleBPM — Architecture

## Overview

SimpleBPM is a lightweight, extensible Business Process Management (BPM) library for .NET 8.
It lets you define, execute, pause, resume, and migrate multi-step business workflows using a fluent C# API or JSON configuration.

The library follows a **layered, handler-based architecture**: process definitions describe *what* to do, node handlers describe *how* to execute each step, and the engine drives the flow from node to node.

---

## High-level layers

```
┌─────────────────────────────────────────────────────────────────┐
│  Client Application                                             │
│  (ICommandHandler / IQueryHandler implementations, DI setup)   │
└───────────────────────────┬─────────────────────────────────────┘
                            │ IFlowService
┌───────────────────────────▼─────────────────────────────────────┐
│  FlowService                                                    │
│  (public API — process lifecycle, migration, search)            │
└───────────────────────────┬─────────────────────────────────────┘
                            │ delegates to
┌───────────────────────────▼─────────────────────────────────────┐
│  FlowEngine                                                     │
│  (execution loop, concurrency lock, definition resolution)      │
└──────┬──────────────────────────────────┬────────────────────────┘
       │                                  │
       ▼                                  ▼
INodeHandler[]                    IProcessRepository
(one per NodeType)                (Oracle / InMemory / Null)
       │
       ▼
IBpmMediateur
(dispatches to client handlers)
```

---

## Project structure

```
SimpleBPM.sln
├── SimpleBPM/                        # Core library
│   ├── Abstractions/                 # Client-facing interfaces
│   │   ├── IBpmMediateur.cs          # Dispatch gateway
│   │   ├── ICommandHandler.cs        # Per-command business logic
│   │   ├── IQueryHandler.cs          # Per-decision query logic
│   │   └── IGestionTache.cs          # Optional task management
│   ├── Definition/
│   │   ├── ProcessBuilder.cs         # Fluent builder API
│   │   └── ProcessJsonLoader.cs      # JSON serialisation / deserialisation
│   ├── Handlers/                     # One handler per NodeType
│   │   ├── INodeHandler.cs
│   │   ├── BusinessNodeHandler.cs
│   │   ├── DecisionNodeHandler.cs
│   │   ├── InteractiveNodeHandler.cs
│   │   ├── WaitForSignalNodeHandler.cs
│   │   ├── WaitUntilDateNodeHandler.cs
│   │   ├── SubProcessNodeHandler.cs
│   │   └── EndNodeHandler.cs
│   ├── Localisation/                 # DI registration
│   │   ├── ServiceCollectionExtensions.cs   # Microsoft.Extensions.DI
│   │   ├── SimpleBPMBuilder.cs              # Fluent options builder
│   │   ├── SimpleBPMAutofacModule.cs        # Autofac module
│   │   └── ProcessMonitoringAutofacModule.cs
│   ├── Migration/                    # Version migration
│   │   ├── ProcessMigration.cs
│   │   ├── ProcessMigrationLoader.cs
│   │   └── ProcessMigrationRunner.cs
│   ├── Nodes/                        # Typed node data classes
│   │   ├── BusinessNode.cs
│   │   ├── DecisionNode.cs
│   │   ├── EndNode.cs
│   │   ├── InteractiveNode.cs
│   │   ├── SubProcessNode.cs
│   │   ├── WaitForSignalNode.cs
│   │   └── WaitUntilDateNode.cs
│   ├── Persistence/                  # Storage layer
│   │   ├── IProcessRepository.cs
│   │   ├── NullProcessRepository.cs
│   │   ├── InMemoryProcessRepository.cs
│   │   ├── OracleProcessRepository.cs
│   │   ├── OracleHistoryRepository.cs
│   │   └── OracleConfiguration.cs
│   ├── BpmMediateur.cs               # O(1) dispatcher
│   ├── ConditionDecision.cs          # Variable-based condition evaluation
│   ├── FiltreVariable.cs             # Variable filter for search
│   ├── FlowEngine.cs                 # Core execution loop
│   ├── FlowService.cs                # Public service facade
│   ├── IFlowService.cs               # Client-facing service interface
│   ├── IProcessMonitor.cs            # Read-only monitoring interface
│   ├── ProcessMonitor.cs             # Monitoring implementation
│   ├── ProcessDefinition.cs          # Process schema (name, version, nodes)
│   ├── ProcessInstance.cs            # Runtime state of a process
│   ├── NodeDefinition.cs             # Base class for node data + result types
│   ├── Processus.cs                  # External DTO (read-only view of an instance)
│   ├── InstanceNode.cs               # External DTO for a node execution record
│   └── NodeInstance.cs              # Internal execution history entry
├── SimpleBPM.ExampleClient/          # Full example (loan approval workflow)
│   ├── CommandHandlers/
│   ├── QueryHandlers/
│   ├── LoanProcessDefinitions.cs
│   ├── LoanTaskManager.cs
│   └── Program.cs
├── SimpleBPM.Blazor/                 # Blazor monitoring dashboard
├── SimpleBPM.Tests/                  # xUnit unit tests
└── schema.sql                        # Manual Oracle DDL script
```

---

## Core domain objects

### ProcessDefinition

Represents the *static schema* of a workflow: its name, version, start node, and a dictionary of named `NodeDefinition` objects.

```
ProcessDefinition
  ├── Name          : string
  ├── Version       : string   (e.g. "1.0")
  ├── StartNodeId   : string
  └── Nodes         : Dictionary<string, NodeDefinition>
```

Multiple versions of the same definition can coexist inside `FlowEngine`. The engine resolves the correct version when executing or continuing an instance.

### NodeDefinition (base)

All node types inherit from this base class.

```
NodeDefinition
  ├── Name                      : string  (unique within a process — acts as the node ID)
  ├── DisplayName               : string
  ├── Type                      : NodeType (enum)
  ├── NextNodeIds               : List<string>
  ├── Parameters                : Dictionary<string, object>
  ├── OnEnterCommandName        : string?
  └── OnEnterCommandParameters  : Dictionary<string, object>
```

Node subtypes add their own fields (e.g. `BusinessNode.CommandName`, `DecisionNode.Conditions`).

### ProcessInstance

Holds the *runtime state* of a single running process.

```
ProcessInstance
  ├── ProcessId         : long
  ├── ParentProcessId   : long?      (set for sub-processes)
  ├── ParentNodeId      : string?
  ├── AggregateId       : long?      (domain aggregate link)
  ├── DefinitionName    : string?
  ├── DefinitionVersion : string?
  ├── Variables         : Dictionary<string, object>   (shared process state)
  ├── CurrentNodeId     : string?
  ├── Status            : ProcessStatus
  ├── ExpectedSignal    : string?
  ├── WaitDate          : DateTime?
  ├── StartedAt         : DateTime
  ├── LastExecutedAt    : DateTime?
  ├── CompletedAt       : DateTime?
  ├── ErrorMessage      : string?
  ├── ExecutionHistory  : List<NodeInstance>
  └── ExecutionLock     : SemaphoreSlim   (internal concurrency guard)
```

### ProcessStatus enum

| Value | Meaning |
|---|---|
| `Running` | Actively executing |
| `WaitingInteraction` | Paused at an `InteractiveNode` |
| `WaitingDate` | Paused at a `WaitUntilDateNode` |
| `WaitingSignal` | Paused at a `WaitForSignalNode` |
| `Completed` | Process finished successfully |
| `Failed` | An unrecoverable error occurred |

---

## Node types

| NodeType | Class | Purpose |
|---|---|---|
| `Business` | `BusinessNode` | Executes a named command via `IBpmMediateur` |
| `Decision` | `DecisionNode` | Routes to the next node based on variable conditions or a query |
| `Interactive` | `InteractiveNode` | Pauses and waits for a human action (`TerminerEtapeAsync`) |
| `WaitForSignal` | `WaitForSignalNode` | Pauses until a named signal is received |
| `WaitUntilDate` | `WaitUntilDateNode` | Pauses until a fixed or dynamic date |
| `SubProcess` | `SubProcessNode` | Runs a nested `ProcessDefinition` with variable mapping |
| `End` | `EndNode` | Explicitly terminates a branch; marks the instance `Completed` |

---

## Node handlers

Each `NodeType` is served by a dedicated `INodeHandler`.

```csharp
public interface INodeHandler
{
    NodeType NodeType { get; }
    Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance);
    Task OnLeaveAsync(NodeDefinition node, ProcessInstance instance); // default: no-op
}
```

`NodeExecutionResult` carries three fields:

| Field | Type | Meaning |
|---|---|---|
| `IsCompleted` | `bool` | `false` = handler failed (engine marks instance `Failed`) |
| `RequiresStop` | `bool` | `true` = engine stops and saves state (waiting nodes) |
| `NextNodeId` | `string?` | Name of the next node to execute |

### Handler registration

The default handlers (`End`, `Interactive`, `WaitForSignal`, `WaitUntilDate`, `SubProcess`) are auto-registered by `FlowEngine`'s constructor. `BusinessNodeHandler` and `DecisionNodeHandler` require an `IBpmMediateur` and must be registered by the DI setup.

---

## FlowEngine — execution loop

`FlowEngine` drives a single process through its nodes. It is stateless beyond its registered definitions and handlers.

### Execution flow (simplified)

```
ExecuteAsync(instance)
  └─ ExecutionLock (SemaphoreSlim) — prevents concurrent execution of the same instance
      └─ loop:
           current = instance.CurrentNodeId ?? definition.StartNodeId
           node    = definition.GetNode(current)
           handler = _handlers[node.Type]

           result  = handler.HandleAsync(node, instance)

           if !result.IsCompleted  → status = Failed, save, return
           if  result.RequiresStop → status = Waiting*, save, return
           else                    → current = result.NextNodeId (continue loop)

           if current == null      → status = Completed, save, return
```

### Continue / Signal

- **`ContinueAsync`** — Called after a user completes an interactive step. Calls `OnLeaveAsync` on the current node handler, then re-enters the execution loop from `NextNodeId`.
- **`SignalAsync`** — Called to unblock a `WaitingSignal` instance. If the signal name matches `instance.ExpectedSignal`, delegates to `ContinueAsync`.

### Multi-definition / multi-version support

`FlowEngine` stores definitions in a `Dictionary<string, List<ProcessDefinition>>` keyed by name. When resolving:

1. If `instance.DefinitionVersion` is set → exact version lookup.
2. If only `instance.DefinitionName` is set → latest semantic version.
3. If neither is set and exactly one definition is registered → uses that one.

---

## Decision node — condition evaluation

`DecisionNode` supports two evaluation modes, selected at definition time:

### Mode 1: Variable conditions (recommended)

Conditions are evaluated in declaration order. The first matching condition wins.

```
DecisionNode.Conditions : List<ConditionDecision>
  each ConditionDecision:
    ├── NomVariable  : string
    ├── Valeur       : object?
    ├── Operateur    : OperateurFiltre
    ├── TypeDonnee   : TypeDonnee
    └── NoeudCible   : string
```

Supported operators: `Egal`, `Different`, `Superieur`, `SuperieurOuEgal`, `Inferieur`, `InferieurOuEgal`, `Contient`, `CommencePar`, `FinitPar`

Supported data types: `Texte`, `Nombre`, `Date`, `Booleen`

A `NoeudParDefaut` can be set as a fallback when no condition matches.

### Mode 2: External query

If `Conditions` is empty, the handler calls `IBpmMediateur.EvaluateDecisionAsync(queryName, ...)`.
The returned string is matched against `DecisionNode.ConditionToNodeId` to find the next node.

---

## Sub-process execution

`SubProcessNodeHandler` checks whether a child instance already exists (for resumption after a pause):

```
exists? → resume via ContinueAsync on a child FlowEngine
no?     → create new ProcessInstance (child)
          apply InputMapping (parent vars → child vars)
          run child via ExecuteAsync

after child completes:
  apply OutputMapping (child vars → parent vars)
  return NextNodeId of the SubProcessNode
if child pauses:
  return RequiresStop = true (parent also pauses)
```

The child shares the same `IProcessRepository` and `_handlers` registry as the parent engine.

---

## IBpmMediateur and handler dispatch

`IBpmMediateur` is the bridge between the engine and client business logic.

```csharp
public interface IBpmMediateur
{
    Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null);

    Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null);
}
```

The built-in `BpmMediateur` implementation (registered automatically by `AddCommandHandlers`) maintains two `Dictionary<string, ...>` maps built at startup from the registered `ICommandHandler` and `IQueryHandler` singletons — giving O(1) dispatch at runtime.

If a handler is not found for a given command or decision name, `BpmMediateur` throws `InvalidOperationException`.

---

## Persistence layer

All storage goes through `IProcessRepository`:

```csharp
public interface IProcessRepository
{
    Task<long> ObtenirSequenceAsync(string nomSequence);
    Task SaveProcessInstanceAsync(ProcessInstance instance);
    Task<ProcessInstance?> GetProcessInstanceAsync(long processId);
    Task UpdateProcessInstanceAsync(ProcessInstance instance);
    Task DeleteProcessInstanceAsync(long processId);
    Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres);
    Task<(NodeInstance History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId);
    Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeId);
    Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId);
    Task<List<ProcessInstance>> GetAllProcessInstancesAsync();
}
```

Three implementations are provided:

| Implementation | Usage |
|---|---|
| `NullProcessRepository` | No persistence (singleton, used when no repo is configured) |
| `InMemoryProcessRepository` | Thread-safe in-memory store with `ConcurrentDictionary` — great for testing |
| `OracleProcessRepository` | Production Oracle store via Dapper; requires an `IDbConnection` provided by the client |

### Oracle tables

The table prefix is validated (3–10 uppercase letters) and applied to all table names:

| Table | Purpose |
|---|---|
| `{PREFIX}_PROCESS_CONTEXT` | One row per process instance (state, variables as JSON, dates) |
| `{PREFIX}_HISTORIQUE_EXECUTION_NOEUD` | One row per executed node (audit trail) |

Oracle sequences (`SEQ_PROCESSUS`, `{PREFIX}_SEQ_HISTORIQUE`) generate all primary keys.

---

## DI registration options

SimpleBPM supports both Microsoft DI and Autofac.

### Microsoft DI — unified builder (recommended)

```csharp
services.AddSimpleBPM(options =>
{
    options.ScanHandlers(Assembly.GetExecutingAssembly());   // discover ICommandHandler / IQueryHandler
    options.UseTaskManager<MyTaskManager>();                  // optional IGestionTache
    options.UseOracle("ABC");                                 // or omit for in-memory
    options.AddProcess(MyProcessDefinitions.CreateProcess()); // register definitions
});
```

### Autofac module

```csharp
builder.RegisterModule(new SimpleBPMAutofacModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<MyTaskManager>();
    module.UseOracle("ABC");
    module.AddProcess(MyProcessDefinitions.CreateProcess());
}));
```

### Monitoring-only (read-only dashboard)

```csharp
// Microsoft DI
services.AddProcessMonitoring();

// Autofac
builder.RegisterModule(new ProcessMonitoringAutofacModule());
```

This registers only `IProcessMonitor` — no engine, no mediateur, no node handlers.

---

## Process monitoring

`IProcessMonitor` provides read-only observation of all running and completed instances without touching the execution engine.

```csharp
public interface IProcessMonitor
{
    List<ProcessDefinition> GetDefinitions();
    Task<List<Processus>> GetAllInstancesAsync();
    Task<List<Processus>> GetRootInstancesAsync();
    Task<List<Processus>> GetAllDescendantsAsync(long processId);
    Task<List<Processus>> GetInstancesByStatusAsync(ProcessStatus status);
    Task<Processus> GetInstanceAsync(long processId);
    Task<List<NodeInstance>> GetExecutionHistoryAsync(long processId);
    Task<Dictionary<ProcessStatus, int>> GetStatusSummaryAsync();
}
```

`Processus` and `InstanceNode` are external DTOs (read-only views) exposed to consumers — clients never touch `ProcessInstance` directly.

---

## Version migration

`ProcessMigration` + `ProcessMigrationRunner` allow migrating paused instances to a new process version without data loss.

Only instances in a waiting state (`WaitingInteraction`, `WaitingSignal`, `WaitingDate`) can be migrated.

Migration maps nodes by name. If a node name changes, an explicit `NodeMapping` entry is required. Variable transformations (`set`, `rename`, `remove`) are applied atomically.

```
ProcessMigration
  ├── FromVersion       : string
  ├── ToVersion         : string
  ├── NodeMappings      : Dictionary<string, string>      (old name → new name)
  └── VariableTransforms: List<VariableTransform>
```

---

## Concurrency model

Each `ProcessInstance` carries an internal `SemaphoreSlim(1,1)` (`ExecutionLock`). `FlowEngine` acquires this lock before running `ExecuteAsync`, `ContinueAsync`, or `SignalAsync`, preventing two concurrent calls from corrupting the same instance's state.

This is complementary to — not a substitute for — database-level locking in the Oracle repository.

---

## Data flow summary

```
Client calls IFlowService.CreateProcessInstanceAsync("OrderProcess", variables)
  │
  ▼
FlowService allocates a new ProcessInstance (ID from DB sequence)
  │
  ▼
FlowEngine.ExecuteAsync(instance)
  │  loop over nodes:
  │    BusinessNode   → IBpmMediateur.ExecuteCommandAsync → ICommandHandler
  │    DecisionNode   → evaluate conditions OR IBpmMediateur.EvaluateDecisionAsync → IQueryHandler
  │    InteractiveNode → save state, return WaitingInteraction
  │    WaitForSignal  → save state, return WaitingSignal
  │    WaitUntilDate  → save state, return WaitingDate
  │    SubProcessNode → recurse into child FlowEngine
  │    EndNode        → status = Completed, return
  │
  ▼
IProcessRepository.UpdateProcessInstanceAsync(instance)
  (written to Oracle or InMemory after every node execution)
```
