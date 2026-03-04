# SimpleBPM — Architecture

## Vue d'ensemble

SimpleBPM est une librairie BPM (Business Process Management) légère et extensible pour .NET 8.
Elle permet de définir, exécuter, mettre en pause, reprendre et migrer des workflows métier multi-étapes via une API C# fluide ou une configuration JSON.

La librairie suit une **architecture en couches orientée handlers** : les définitions de processus décrivent *quoi* faire, les handlers de nœuds décrivent *comment* exécuter chaque étape, et le moteur pilote le flux de nœud en nœud.

---

## Couches de haut niveau

```
┌─────────────────────────────────────────────────────────────────┐
│  Application cliente                                            │
│  (implémentations ICommandHandler / IQueryHandler, config DI)  │
└───────────────────────────┬─────────────────────────────────────┘
                            │ IFlowService
┌───────────────────────────▼─────────────────────────────────────┐
│  FlowService                                                    │
│  (API publique — cycle de vie, migration, recherche)            │
└───────────────────────────┬─────────────────────────────────────┘
                            │ délègue à
┌───────────────────────────▼─────────────────────────────────────┐
│  FlowEngine                                                     │
│  (boucle d'exécution, verrou de concurrence, résolution déf.)  │
└──────┬──────────────────────────────────┬────────────────────────┘
       │                                  │
       ▼                                  ▼
INodeHandler[]                    IProcessRepository
(un par NodeType)                 (Oracle / Mémoire / Null)
       │
       ▼
IBpmMediateur
(dispatch vers les handlers clients)
```

---

## Structure du projet

```
SimpleBPM.sln
├── SimpleBPM/                        # Librairie principale
│   ├── Abstractions/                 # Interfaces exposées au client
│   │   ├── IBpmMediateur.cs          # Passerelle de dispatch
│   │   ├── ICommandHandler.cs        # Logique métier par commande
│   │   ├── IQueryHandler.cs          # Logique de décision par requête
│   │   └── IGestionTache.cs          # Gestion de tâches (optionnel)
│   ├── Definition/
│   │   ├── ProcessBuilder.cs         # API fluide de construction
│   │   └── ProcessJsonLoader.cs      # Sérialisation / désérialisation JSON
│   ├── Handlers/                     # Un handler par NodeType
│   │   ├── INodeHandler.cs
│   │   ├── BusinessNodeHandler.cs
│   │   ├── DecisionNodeHandler.cs
│   │   ├── InteractiveNodeHandler.cs
│   │   ├── WaitForSignalNodeHandler.cs
│   │   ├── WaitUntilDateNodeHandler.cs
│   │   ├── SubProcessNodeHandler.cs
│   │   └── EndNodeHandler.cs
│   ├── Localisation/                 # Enregistrement DI
│   │   ├── ServiceCollectionExtensions.cs   # Microsoft.Extensions.DI
│   │   ├── SimpleBPMBuilder.cs              # Builder d'options fluide
│   │   ├── SimpleBPMAutofacModule.cs        # Module Autofac
│   │   └── ProcessMonitoringAutofacModule.cs
│   ├── Migration/                    # Migration de version
│   │   ├── ProcessMigration.cs
│   │   ├── ProcessMigrationLoader.cs
│   │   └── ProcessMigrationRunner.cs
│   ├── Nodes/                        # Classes de données des nœuds typés
│   │   ├── BusinessNode.cs
│   │   ├── DecisionNode.cs
│   │   ├── EndNode.cs
│   │   ├── InteractiveNode.cs
│   │   ├── SubProcessNode.cs
│   │   ├── WaitForSignalNode.cs
│   │   └── WaitUntilDateNode.cs
│   ├── Persistence/                  # Couche de stockage
│   │   ├── IProcessRepository.cs
│   │   ├── NullProcessRepository.cs
│   │   ├── InMemoryProcessRepository.cs
│   │   ├── OracleProcessRepository.cs
│   │   ├── OracleHistoryRepository.cs
│   │   └── OracleConfiguration.cs
│   ├── BpmMediateur.cs               # Dispatcher O(1)
│   ├── ConditionDecision.cs          # Évaluation de conditions sur variables
│   ├── FiltreVariable.cs             # Filtre de recherche par variable
│   ├── FlowEngine.cs                 # Boucle d'exécution principale
│   ├── FlowService.cs                # Façade de service publique
│   ├── IFlowService.cs               # Interface de service côté client
│   ├── IProcessMonitor.cs            # Interface de surveillance en lecture seule
│   ├── ProcessMonitor.cs             # Implémentation de la surveillance
│   ├── ProcessDefinition.cs          # Schéma du processus (nom, version, nœuds)
│   ├── ProcessInstance.cs            # État d'exécution d'un processus
│   ├── NodeDefinition.cs             # Classe de base des nœuds + types de résultat
│   ├── Processus.cs                  # DTO externe (vue en lecture seule d'une instance)
│   ├── InstanceNode.cs               # DTO externe d'une entrée d'historique
│   └── NodeInstance.cs               # Entrée d'historique interne
├── SimpleBPM.ExampleClient/          # Exemple complet (workflow d'approbation de prêt)
│   ├── CommandHandlers/
│   ├── QueryHandlers/
│   ├── LoanProcessDefinitions.cs
│   ├── LoanTaskManager.cs
│   └── Program.cs
├── SimpleBPM.Blazor/                 # Tableau de bord Blazor de surveillance
├── SimpleBPM.Tests/                  # Tests unitaires xUnit
└── schema.sql                        # Script DDL Oracle manuel
```

---

## Objets du domaine central

### ProcessDefinition

Représente le *schéma statique* d'un workflow : son nom, sa version, le nœud de départ et un dictionnaire nommé de `NodeDefinition`.

```
ProcessDefinition
  ├── Name          : string
  ├── Version       : string   (ex. "1.0")
  ├── StartNodeId   : string
  └── Nodes         : Dictionary<string, NodeDefinition>
```

Plusieurs versions d'une même définition peuvent coexister dans `FlowEngine`. Le moteur résout la version correcte lors de l'exécution ou de la reprise d'une instance.

### NodeDefinition (base)

Tous les types de nœuds héritent de cette classe de base.

```
NodeDefinition
  ├── Name                      : string  (unique dans un processus — sert d'identifiant)
  ├── DisplayName               : string
  ├── Type                      : NodeType (enum)
  ├── NextNodeIds               : List<string>
  ├── Parameters                : Dictionary<string, object>
  ├── OnEnterCommandName        : string?
  └── OnEnterCommandParameters  : Dictionary<string, object>
```

Les sous-types ajoutent leurs propres champs (ex. `BusinessNode.CommandName`, `DecisionNode.Conditions`).

### ProcessInstance

Contient l'*état d'exécution* d'un processus en cours.

```
ProcessInstance
  ├── ProcessId         : long
  ├── ParentProcessId   : long?      (renseigné pour les sous-processus)
  ├── ParentNodeId      : string?
  ├── AggregateId       : long?      (lien vers l'agrégat métier)
  ├── DefinitionName    : string?
  ├── DefinitionVersion : string?
  ├── Variables         : Dictionary<string, object>   (état partagé du processus)
  ├── CurrentNodeId     : string?
  ├── Status            : ProcessStatus
  ├── ExpectedSignal    : string?
  ├── WaitDate          : DateTime?
  ├── StartedAt         : DateTime
  ├── LastExecutedAt    : DateTime?
  ├── CompletedAt       : DateTime?
  ├── ErrorMessage      : string?
  ├── ExecutionHistory  : List<NodeInstance>
  └── ExecutionLock     : SemaphoreSlim   (verrou interne de concurrence)
```

### Enum ProcessStatus

| Valeur | Signification |
|---|---|
| `Running` | En cours d'exécution |
| `WaitingInteraction` | En pause sur un `InteractiveNode` |
| `WaitingDate` | En pause sur un `WaitUntilDateNode` |
| `WaitingSignal` | En pause sur un `WaitForSignalNode` |
| `Completed` | Processus terminé avec succès |
| `Failed` | Une erreur irrécupérable est survenue |

---

## Types de nœuds

| NodeType | Classe | Rôle |
|---|---|---|
| `Business` | `BusinessNode` | Exécute une commande nommée via `IBpmMediateur` |
| `Decision` | `DecisionNode` | Route vers le nœud suivant selon des conditions sur les variables ou une requête |
| `Interactive` | `InteractiveNode` | Met en pause et attend une action humaine (`TerminerEtapeAsync`) |
| `WaitForSignal` | `WaitForSignalNode` | Met en pause jusqu'à la réception d'un signal nommé |
| `WaitUntilDate` | `WaitUntilDateNode` | Met en pause jusqu'à une date fixe ou dynamique |
| `SubProcess` | `SubProcessNode` | Exécute une `ProcessDefinition` imbriquée avec mapping de variables |
| `End` | `EndNode` | Termine explicitement une branche ; marque l'instance `Completed` |

---

## Handlers de nœuds

Chaque `NodeType` est pris en charge par un `INodeHandler` dédié.

```csharp
public interface INodeHandler
{
    NodeType NodeType { get; }
    Task<NodeExecutionResult> HandleAsync(NodeDefinition node, ProcessInstance instance);
    Task OnLeaveAsync(NodeDefinition node, ProcessInstance instance); // par défaut : no-op
}
```

`NodeExecutionResult` porte trois champs :

| Champ | Type | Signification |
|---|---|---|
| `IsCompleted` | `bool` | `false` = le handler a échoué (le moteur marque l'instance `Failed`) |
| `RequiresStop` | `bool` | `true` = le moteur s'arrête et sauvegarde l'état (nœuds d'attente) |
| `NextNodeId` | `string?` | Nom du prochain nœud à exécuter |

### Enregistrement des handlers

Les handlers par défaut (`End`, `Interactive`, `WaitForSignal`, `WaitUntilDate`, `SubProcess`) sont auto-enregistrés par le constructeur de `FlowEngine`. `BusinessNodeHandler` et `DecisionNodeHandler` nécessitent un `IBpmMediateur` et doivent être enregistrés par la configuration DI.

---

## FlowEngine — boucle d'exécution

`FlowEngine` pilote un processus à travers ses nœuds. Il est sans état au-delà de ses définitions et handlers enregistrés.

### Flux d'exécution (simplifié)

```
ExecuteAsync(instance)
  └─ ExecutionLock (SemaphoreSlim) — empêche l'exécution concurrente de la même instance
      └─ boucle :
           current = instance.CurrentNodeId ?? definition.StartNodeId
           node    = definition.GetNode(current)
           handler = _handlers[node.Type]

           result  = handler.HandleAsync(node, instance)

           si !result.IsCompleted  → status = Failed, sauvegarde, retour
           si  result.RequiresStop → status = Waiting*, sauvegarde, retour
           sinon                   → current = result.NextNodeId (continue la boucle)

           si current == null      → status = Completed, sauvegarde, retour
```

### Continue / Signal

- **`ContinueAsync`** — Appelé après qu'un utilisateur complète une étape interactive. Appelle `OnLeaveAsync` sur le handler du nœud courant, puis reprend la boucle d'exécution à partir du `NextNodeId`.
- **`SignalAsync`** — Appelé pour débloquer une instance en `WaitingSignal`. Si le nom du signal correspond à `instance.ExpectedSignal`, délègue à `ContinueAsync`.

### Support multi-définitions / multi-versions

`FlowEngine` stocke les définitions dans un `Dictionary<string, List<ProcessDefinition>>` indexé par nom. Lors de la résolution :

1. Si `instance.DefinitionVersion` est renseignée → recherche de la version exacte.
2. Si seul `instance.DefinitionName` est renseigné → version sémantique la plus récente.
3. Si aucun n'est renseigné et qu'une seule définition est enregistrée → utilise celle-ci.

---

## Nœud de décision — évaluation des conditions

`DecisionNode` supporte deux modes d'évaluation, choisis à la définition :

### Mode 1 : Conditions sur variables (recommandé)

Les conditions sont évaluées dans l'ordre de déclaration. La première condition vraie l'emporte.

```
DecisionNode.Conditions : List<ConditionDecision>
  chaque ConditionDecision :
    ├── NomVariable  : string
    ├── Valeur       : object?
    ├── Operateur    : OperateurFiltre
    ├── TypeDonnee   : TypeDonnee
    └── NoeudCible   : string
```

Opérateurs disponibles : `Egal`, `Different`, `Superieur`, `SuperieurOuEgal`, `Inferieur`, `InferieurOuEgal`, `Contient`, `CommencePar`, `FinitPar`

Types de données disponibles : `Texte`, `Nombre`, `Date`, `Booleen`

Un `NoeudParDefaut` peut être défini en repli si aucune condition ne correspond.

### Mode 2 : Requête externe

Si `Conditions` est vide, le handler appelle `IBpmMediateur.EvaluateDecisionAsync(queryName, ...)`.
La chaîne retournée est comparée à `DecisionNode.ConditionToNodeId` pour trouver le nœud suivant.

---

## Exécution d'un sous-processus

`SubProcessNodeHandler` vérifie si une instance enfant existe déjà (pour une reprise après pause) :

```
existe ? → reprise via ContinueAsync sur un FlowEngine enfant
non ?    → créer un nouveau ProcessInstance (enfant)
           appliquer InputMapping (variables parent → enfant)
           exécuter l'enfant via ExecuteAsync

après la fin de l'enfant :
  appliquer OutputMapping (variables enfant → parent)
  retourner NextNodeId du SubProcessNode
si l'enfant se met en pause :
  retourner RequiresStop = true (le parent se met aussi en pause)
```

L'enfant partage le même `IProcessRepository` et le même registre `_handlers` que le moteur parent.

---

## IBpmMediateur et dispatch des handlers

`IBpmMediateur` est le pont entre le moteur et la logique métier cliente.

```csharp
public interface IBpmMediateur
{
    Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null);

    Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null);
}
```

L'implémentation intégrée `BpmMediateur` (enregistrée automatiquement par `AddCommandHandlers`) maintient deux `Dictionary<string, ...>` construits au démarrage à partir des singletons `ICommandHandler` et `IQueryHandler` enregistrés — offrant un dispatch en O(1) à l'exécution.

Si aucun handler n'est trouvé pour une commande ou une décision donnée, `BpmMediateur` lève une `InvalidOperationException`.

---

## Couche de persistance

Tout le stockage passe par `IProcessRepository` :

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

Trois implémentations sont fournies :

| Implémentation | Usage |
|---|---|
| `NullProcessRepository` | Aucune persistance (singleton, utilisé sans repository configuré) |
| `InMemoryProcessRepository` | Stockage en mémoire thread-safe avec `ConcurrentDictionary` — idéal pour les tests |
| `OracleProcessRepository` | Stockage Oracle en production via Dapper ; nécessite une `IDbConnection` fournie par le client |

### Tables Oracle

Le préfixe de table est validé (3 à 10 lettres majuscules) et appliqué à tous les noms de tables :

| Table | Rôle |
|---|---|
| `{PREFIXE}_PROCESS_CONTEXT` | Une ligne par instance de processus (état, variables en JSON, dates) |
| `{PREFIXE}_HISTORIQUE_EXECUTION_NOEUD` | Une ligne par nœud exécuté (journal d'audit) |

Les séquences Oracle (`SEQ_PROCESSUS`, `{PREFIXE}_SEQ_HISTORIQUE`) génèrent toutes les clés primaires.

---

## Options d'enregistrement DI

SimpleBPM supporte Microsoft DI et Autofac.

### Microsoft DI — builder unifié (recommandé)

```csharp
services.AddSimpleBPM(options =>
{
    options.ScanHandlers(Assembly.GetExecutingAssembly());   // découvrir ICommandHandler / IQueryHandler
    options.UseTaskManager<MyGestionTache>();                 // IGestionTache optionnel
    options.UseOracle("ABC");                                 // ou omettre pour la mémoire
    options.AddProcess(MyProcessDefinitions.CreateProcess()); // enregistrer les définitions
});
```

### Module Autofac

```csharp
builder.RegisterModule(new SimpleBPMAutofacModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<MyGestionTache>();
    module.UseOracle("ABC");
    module.AddProcess(MyProcessDefinitions.CreateProcess());
}));
```

### Surveillance seule (tableau de bord en lecture seule)

```csharp
// Microsoft DI
services.AddProcessMonitoring();

// Autofac
builder.RegisterModule(new ProcessMonitoringAutofacModule());
```

Enregistre uniquement `IProcessMonitor` — aucun moteur, aucun médiateur, aucun handler de nœud.

---

## Surveillance des processus

`IProcessMonitor` offre une observation en lecture seule de toutes les instances en cours ou terminées, sans toucher au moteur d'exécution.

```csharp
public interface IProcessMonitor
{
    List<ProcessDefinition>              GetDefinitions();
    Task<List<Processus>>                GetAllInstancesAsync();
    Task<List<Processus>>                GetRootInstancesAsync();
    Task<List<Processus>>                GetAllDescendantsAsync(long processId);
    Task<List<Processus>>                GetInstancesByStatusAsync(ProcessStatus status);
    Task<Processus>                      GetInstanceAsync(long processId);
    Task<List<NodeInstance>>             GetExecutionHistoryAsync(long processId);
    Task<Dictionary<ProcessStatus, int>> GetStatusSummaryAsync();
}
```

`Processus` et `InstanceNode` sont des DTOs externes en lecture seule exposés aux consommateurs — les clients ne manipulent jamais `ProcessInstance` directement.

---

## Migration de version

`ProcessMigration` + `ProcessMigrationRunner` permettent de migrer des instances en pause vers une nouvelle version de processus sans perte de données.

Seules les instances en état d'attente (`WaitingInteraction`, `WaitingSignal`, `WaitingDate`) peuvent être migrées.

La migration mappe les nœuds par nom. Si un nom de nœud change, une entrée `NodeMapping` explicite est requise. Les transformations de variables (`set`, `rename`, `remove`) sont appliquées de manière atomique.

```
ProcessMigration
  ├── FromVersion        : string
  ├── ToVersion          : string
  ├── NodeMappings       : Dictionary<string, string>      (ancien nom → nouveau nom)
  └── VariableTransforms : List<VariableTransform>
```

---

## Modèle de concurrence

Chaque `ProcessInstance` porte un `SemaphoreSlim(1,1)` interne (`ExecutionLock`). `FlowEngine` acquiert ce verrou avant d'exécuter `ExecuteAsync`, `ContinueAsync` ou `SignalAsync`, empêchant deux appels concurrents de corrompre l'état de la même instance.

Ce mécanisme est complémentaire — et non substitutif — au verrouillage au niveau de la base de données dans le repository Oracle.

---

## Résumé du flux de données

```
Le client appelle IFlowService.CreateProcessInstanceAsync("OrderProcess", variables)
  │
  ▼
FlowService alloue un nouveau ProcessInstance (ID issu d'une séquence DB)
  │
  ▼
FlowEngine.ExecuteAsync(instance)
  │  boucle sur les nœuds :
  │    BusinessNode    → IBpmMediateur.ExecuteCommandAsync → ICommandHandler
  │    DecisionNode    → évalue les conditions OU IBpmMediateur.EvaluateDecisionAsync → IQueryHandler
  │    InteractiveNode → sauvegarde l'état, retourne WaitingInteraction
  │    WaitForSignal   → sauvegarde l'état, retourne WaitingSignal
  │    WaitUntilDate   → sauvegarde l'état, retourne WaitingDate
  │    SubProcessNode  → récursion dans un FlowEngine enfant
  │    EndNode         → status = Completed, retour
  │
  ▼
IProcessRepository.UpdateProcessInstanceAsync(instance)
  (écrit dans Oracle ou en mémoire après chaque exécution de nœud)
```
