# SimpleBPM — Architecture

## Vue d'ensemble

SimpleBPM est un moteur BPM (Business Process Management) léger en .NET 8 organisé autour de quatre couches : définition, exécution, persistance et intégration client.

```
┌─────────────────────────────────────────────────┐
│                  Client (IFlowService)           │
├─────────────────────────────────────────────────┤
│  FlowEngine          │  ProcessMonitor           │
│  (orchestration)     │  (lecture seule)          │
├──────────────────────┴───────────────────────────┤
│  Node Handlers  (un handler par type de nœud)    │
├─────────────────────────────────────────────────┤
│  Persistence  (IProcessRepository / IDefinition  │
│               Repository)                        │
└─────────────────────────────────────────────────┘
```

## Structure du projet

```
SimpleBPM.sln
├── SimpleBPM/                         # Librairie principale
│   ├── Abstractions/                  # Interfaces client
│   │   ├── IBpmMediateur.cs           # Dispatche commandes & décisions
│   │   ├── IBpmCommandHandler.cs      # Handler de commande métier
│   │   ├── IBpmQueryHandler.cs        # Handler de décision/requête
│   │   └── IGestionTache.cs           # Gestion de tâches (optionnel)
│   ├── Definition/
│   │   ├── ProcessBuilder.cs          # Fluent builder de définitions
│   │   └── ProcessJsonLoader.cs       # Sérialisation JSON
│   ├── Handlers/                      # Un handler par NodeType
│   │   ├── BusinessNodeHandler.cs
│   │   ├── DecisionNodeHandler.cs
│   │   ├── InteractiveNodeHandler.cs
│   │   ├── WaitUntilDateNodeHandler.cs
│   │   ├── WaitForSignalNodeHandler.cs
│   │   ├── SubProcessNodeHandler.cs
│   │   └── EndNodeHandler.cs
│   ├── Localisation/                  # Enregistrement DI
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── SimpleBPMAutofacModule.cs
│   │   ├── ProcessMonitoringAutofacModule.cs
│   │   └── SimpleBPMBuilder.cs
│   ├── Migration/                     # Migration de version
│   │   ├── ProcessMigration.cs
│   │   ├── ProcessMigrationRunner.cs
│   │   ├── ProcessMigrationLoader.cs
│   │   ├── VariableTransform.cs
│   │   └── MigrationResult.cs
│   ├── Nodes/                         # Classes de données des nœuds
│   ├── Persistence/
│   │   ├── IProcessRepository.cs
│   │   ├── IDefinitionRepository.cs
│   │   ├── OracleProcessRepository.cs
│   │   ├── OracleDefinitionRepository.cs
│   │   ├── OracleHistoryRepository.cs
│   │   ├── InMemoryProcessRepository.cs
│   │   ├── InMemoryDefinitionRepository.cs
│   │   └── OracleConfiguration.cs
│   ├── BpmMediateur.cs                # Dispatch vers handlers individuels
│   ├── FlowEngine.cs                  # Moteur d'exécution
│   ├── FlowService.cs                 # Façade publique (IFlowService)
│   ├── ProcessMonitor.cs              # Surveillance en lecture seule
│   ├── ProcessDefinition.cs
│   ├── ProcessInstance.cs
│   └── NodeDefinition.cs
├── SimpleBPM.Blazor/                  # Dashboard de monitoring Blazor
├── SimpleBPM.ExampleClient/           # Exemple d'intégration complet
│   ├── CommandHandlers/               # Implémentations IBpmCommandHandler
│   ├── QueryHandlers/                 # Implémentations IBpmQueryHandler
│   └── Program.cs
├── SimpleBPM.Tests/                   # Tests unitaires xUnit
└── schema.sql                         # Script DDL Oracle
```

## Composants clés

### FlowEngine

Le moteur d'exécution central. Il :

- Maintient un dictionnaire en mémoire `name → [version₁, version₂, …]` de définitions
- Résout une définition depuis `IDefinitionRepository` si elle est absente en mémoire et la met en cache
- Exécute les nœuds en boucle, délégant à l'`INodeHandler` correspondant
- Sauvegarde/met à jour l'instance dans `IProcessRepository` après chaque transition

```
ExecuteAsync(instance)
  └─ ResolveDefinitionAsync()          ← mémoire ou BD
       └─ while (nextNodeId != null)
            └─ handler.HandleAsync()   ← délégation au handler
                 └─ UpdateAsync()      ← persistance
```

### Node Handlers

Chaque type de nœud a un handler dédié qui implémente `INodeHandler` :

| Type | Handler | Comportement |
|------|---------|--------------|
| Business | `BusinessNodeHandler` | Appelle `IBpmMediateur.ExecuteCommandAsync` |
| Decision | `DecisionNodeHandler` | Évalue conditions ou `IBpmMediateur.EvaluateDecisionAsync`, route |
| Interactive | `InteractiveNodeHandler` | Arrête l'exécution, crée une tâche via `IGestionTache` |
| WaitUntilDate | `WaitUntilDateNodeHandler` | Arrête l'exécution jusqu'à une date |
| WaitForSignal | `WaitForSignalNodeHandler` | Arrête l'exécution jusqu'à un signal nommé |
| SubProcess | `SubProcessNodeHandler` | Lance un processus enfant, attend sa complétion |
| End | `EndNodeHandler` | Marque l'instance comme `Completed` |

### Persistance

Deux référentiels indépendants :

- **`IProcessRepository`** : cycle de vie des instances (`ProcessInstance`) — création, mise à jour, recherche, suppression
- **`IDefinitionRepository`** : versioning des définitions (`ProcessDefinition`) — sauvegarde, chargement, liste, suppression

Implémentations disponibles :

| Interface | Oracle | Mémoire |
|-----------|--------|---------|
| `IProcessRepository` | `OracleProcessRepository` | `InMemoryProcessRepository` |
| `IDefinitionRepository` | `OracleDefinitionRepository` | `InMemoryDefinitionRepository` |

### BpmMediateur

Dispatcher O(1) qui traduit les noms de commandes/décisions vers les handlers individuels :

```
BpmMediateur
  ├─ _commandHandlers: { "ValidateOrder" → ValidateOrderHandler, … }
  └─ _queryHandlers:   { "CreditDecision" → CreditDecisionHandler, … }
```

Alimenté par `AddCommandHandlers()` qui scanne les assemblies via réflexion.

### Migration de version

Permet de migrer des instances **en attente** (`WaitingInteraction`, `WaitingSignal`, `WaitingDate`) vers une nouvelle version de définition :

1. Mappage des nœuds (ancien nom → nouveau nom)
2. Transformations de variables (set / rename / remove)
3. Mise à jour de `DefinitionVersion` et `CurrentNodeId` sur l'instance

### Tables Oracle

| Table | Rôle |
|-------|------|
| `{PREFIX}_DEFINITION` | Définitions versionnées (JSON) |
| `{PREFIX}_PROCESS_CONTEXT` | Instances d'exécution |
| `{PREFIX}_HISTORIQUE_EXECUTION_NOEUD` | Historique nœud par nœud |

Les séquences `{PREFIX}_SEQ_PROCESSUS` et `{PREFIX}_SEQ_HISTORIQUE` génèrent les PK numériques.

## Flux d'exécution

```
Client
  │
  ├─ CreateProcessInstanceAsync("OrderProcess")
  │     ├─ ObtenirSequenceAsync()        → ID unique
  │     ├─ FlowEngine.ExecuteAsync()
  │     │     ├─ ResolveDefinitionAsync() → définition (mémoire ou BD)
  │     │     ├─ [Step1] BusinessHandler  → IBpmMediateur.ExecuteCommandAsync()
  │     │     ├─ [Step2] DecisionHandler  → IBpmMediateur.EvaluateDecisionAsync()
  │     │     └─ [Step3] InteractiveHandler → arrêt, WaitingInteraction
  │     └─ return processId
  │
  ├─ TerminerEtapeEnCoursAsync(processId, variables)
  │     └─ FlowEngine.ContinueAsync()
  │           ├─ OnLeaveAsync() (ferme la tâche via IGestionTache)
  │           └─ ExecuteInternalAsync() → continue le flux
  │
  └─ EnvoyerSignalAsync(processId, "PaymentReceived")
        └─ FlowEngine.SignalAsync()
              └─ ContinueInternalAsync() → reprend si signal attendu
```

## Injection de dépendances

SimpleBPM s'enregistre via un point d'entrée unique :

```csharp
// Microsoft DI
services.AddSimpleBPM(options =>
{
    options.ScanHandlers(Assembly.GetExecutingAssembly());
    options.UseOracle("BPM");
    options.AddProcess(myDefinition);
});

// Autofac
builder.RegisterModule(new SimpleBPMAutofacModule(m =>
{
    m.ScanHandlers(Assembly.GetExecutingAssembly());
    m.UseOracle("BPM");
    m.AddProcess(myDefinition);
}));
```

`ScanHandlers()` découvre automatiquement toutes les implémentations de `IBpmCommandHandler` et `IBpmQueryHandler` dans les assemblies indiquées et enregistre un `BpmMediateur` comme `IBpmMediateur`.
