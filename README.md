# SimpleBPM - Librairie BPM simple en C#

Une librairie légère pour gérer des processus métier (BPM) avec différents types de nœuds et persistance Oracle.

## Types de nœuds

- **BusinessNode** : Exécute une commande métier
- **DecisionNode** : Permet de router vers différents nœuds selon le résultat d'une évaluation
- **InteractiveNode** : Arrête le processus en attente d'interaction utilisateur
- **WaitUntilDateNode** : Arrête le processus jusqu'à une date précise
- **WaitForSignalNode** : Arrête le processus en attente d'un signal spécifique
- **SubProcessNode** : Exécute un sous-processus complet avec gestion d'état

## Définition de processus

Deux méthodes sont disponibles pour définir un processus:

### Fluent Builder (recommandé)

API fluide avec IntelliSense et validation à la compilation.

```csharp
using SimpleBPM.Definition;

var process = ProcessBuilder.Create("OrderProcess")
    .Business("ValidateOrder", "Valider la commande")
    .Business("CheckInventory", "Vérifier le stock")
    .Decision("DecideApproval", "Décision", routes => routes
        .When("approved", "ProcessApproved")
        .When("rejected", "ProcessRejected"))
    .Business("ProcessApproved", "Traiter approuvé")
        .Then("WaitPayment")
    .Business("ProcessRejected", "Traiter rejeté")
    .WaitForSignal("WaitPayment", "Attente paiement")
    .Interactive("ManualReview", "Revue manuelle")
    .Build();
```

#### Avec sous-processus inline

```csharp
var process = ProcessBuilder.Create("MainProcess")
    .Business("Start", "Démarrage")
    .SubProcess("Validation", sub => sub
        .Business("ValidateData", "Valider données")
        .Interactive("Approve", "Approbation"))
    .Business("Complete", "Fin")
    .Build();
```

### JSON

Définition déclarative, idéale pour la configuration externe.

```json
{
    "name": "OrderProcess",
    "startNode": "ValidateOrder",
    "nodes": [
        {
            "name": "ValidateOrder",
            "type": "Business",
            "command": "ValidateOrder",
            "next": ["CheckInventory"]
        },
        {
            "name": "CheckInventory",
            "type": "Business",
            "command": "CheckInventory",
            "next": ["DecideApproval"]
        },
        {
            "name": "DecideApproval",
            "type": "Decision",
            "query": "DecideApproval",
            "routes": {
                "approved": "ProcessApproved",
                "rejected": "ProcessRejected"
            }
        }
    ]
}
```

```csharp
using SimpleBPM.Definition;

// Charger depuis JSON
var process = ProcessJsonLoader.FromJson(json);
var process = ProcessJsonLoader.FromJsonFile("process.json");

// Exporter en JSON
var json = ProcessJsonLoader.ToJson(process);
```

Voir `Examples/ExampleWithBuilder.cs` pour un exemple complet.

## Sous-processus

Les sous-processus permettent de décomposer des processus complexes en sous-unités réutilisables.

### Caractéristiques

- **Héritage d'agrégat** : Le sous-processus peut hériter de l'ID d'agrégat du processus parent
- **Mapping explicite** : Les variables sont transférées via `InputMapping` (parent → sous-processus) et `OutputMapping` (sous-processus → parent)
- **Gestion d'état** : Sauvegarde automatique de l'état du sous-processus en cas d'arrêt
- **Reprise** : Capacité à reprendre un sous-processus après une pause
- **Suivi dédié** : Les IDs de sous-processus sont stockés dans `SubProcessIds` (séparé des variables)

### Mapping de variables

Le transfert de données entre processus parent et sous-processus utilise des mappings explicites :

- **InputMapping** : `{ "ParentVar": "SubVar" }` — copie `ParentVar` du parent vers `SubVar` du sous-processus
- **OutputMapping** : `{ "SubResult": "ParentResult" }` — copie `SubResult` du sous-processus vers `ParentResult` du parent

### Exemple avec Fluent Builder

```csharp
var process = ProcessBuilder.Create("MainProcess")
    .Business("Start", "Démarrage")
    .SubProcess("Validation", validationDefinition,
        inputMapping: new() { ["OrderAmount"] = "Amount", ["CustomerId"] = "ClientId" },
        outputMapping: new() { ["ValidationResult"] = "IsValid" })
    .Business("Complete", "Fin")
    .Build();
```

### Exemple avec builder inline

```csharp
var process = ProcessBuilder.Create("MainProcess")
    .Business("Start", "Démarrage")
    .SubProcess("Validation", sub => sub
        .Business("ValidateData", "Valider données")
        .Interactive("Approve", "Approbation"),
        inputMapping: new() { ["OrderAmount"] = "Amount" },
        outputMapping: new() { ["Result"] = "ValidationResult" })
    .Business("Complete", "Fin")
    .Build();
```

### Exemple JSON

```json
{
    "name": "MainProcess",
    "nodes": [
        {
            "name": "Start",
            "type": "Business",
            "command": "Start",
            "next": ["Validation"]
        },
        {
            "name": "Validation",
            "type": "SubProcess",
            "inputMapping": { "OrderAmount": "Amount" },
            "outputMapping": { "Result": "ValidationResult" },
            "subProcess": {
                "name": "ValidationProcess",
                "startNode": "ValidateData",
                "nodes": [
                    { "name": "ValidateData", "type": "Business", "command": "Validate" }
                ]
            },
            "next": ["Complete"]
        },
        {
            "name": "Complete",
            "type": "Business",
            "command": "Complete"
        }
    ]
}
```

Voir `Examples/ExampleWithSubProcess.cs` pour un exemple complet.

## Persistance Oracle

La librairie supporte la persistance dans Oracle avec préfixe de tables personnalisable.

### Configuration

Le repository accepte une `IDbConnection` injectée par le client, compatible avec les containers DI.

```csharp
var oracleConfig = new OracleConfiguration(
    connectionString: "User Id=myuser;Password=mypass;Data Source=localhost:1521/XEPDB1",
    tablePrefix: "ABC" // Préfixe de 3 à 10 lettres
);

// Connexion gérée par le client
using var connection = new OracleConnection(oracleConfig.ConnectionString);
connection.Open();

var repository = new OracleProcessRepository(oracleConfig, connection);
await repository.InitializeDatabaseAsync();
```

### Avec Dependency Injection

```csharp
// Program.cs / Startup.cs
services.AddScoped<IDbConnection>(sp =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return conn;
});
services.AddScoped<OracleConfiguration>(_ => new OracleConfiguration(connectionString, "BPM"));
services.AddScoped<IProcessRepository, OracleProcessRepository>();
services.AddSingleton<ICommandExecutor, MyCommandExecutor>();
services.AddSingleton<INodeHandler>(sp => new BusinessNodeHandler(sp.GetRequiredService<ICommandExecutor>()));
services.AddSingleton<INodeHandler>(sp => new DecisionNodeHandler(sp.GetRequiredService<ICommandExecutor>()));
services.AddSingleton<IGestionTache, MyGestionTache>(); // Optionnel
services.AddSingleton<INodeHandler>(sp => new InteractiveNodeHandler(sp.GetService<IGestionTache>()));
```

**Note** : Le repository ne gère pas le cycle de vie de la connexion. C'est la responsabilité du client (ou du container DI) de l'ouvrir et la fermer.

### Préfixe de tables

- Le préfixe doit contenir entre 3 et 10 lettres
- Seules les lettres sont acceptées (pas de chiffres ou caractères spéciaux)
- Le préfixe est automatiquement converti en majuscules
- Exemple : préfixe "ABC" → table "ABC_PROCESS_CONTEXT"

### Tables créées

- `{PREFIX}_PROCESS_CONTEXT` : Stocke les instances d'exécution des processus
- `{PREFIX}_HISTORIQUE_EXECUTION_NOEUD` : Historique détaillé de chaque étape

## Utilisation

Le client interagit avec la librairie via l'interface `IFlowService`.

```csharp
// Créer une instance de processus
var processId = await flowService.CreateProcessInstance("OrderProcess", new()
{
    ["OrderAmount"] = 1500.00
});

// Obtenir un processus
var processus = await flowService.ObtenirAsync(processId);

// Terminer une étape (nœud interactif) avec du contenu
await flowService.TerminerEtape(nodeInstanceId, new Dictionary<string, object>
{
    ["Decision"] = "approved"
});

// Obtenir les signaux en attente
var signaux = await flowService.ObtenirSignauxEnAttente(processId);

// Rechercher par variable
var resultats = await flowService.RechercherParVariable(new() { ["OrderAmount"] = 1500.00 });

// Obtenir les sous-processus enfants
var enfants = await flowService.ObtenirEnfants(processId);

// Obtenir un nœud d'instance
var noeud = await flowService.Obtenir(nodeInstanceId);
```

### Avec Dependency Injection

```csharp
services.AddScoped<IFlowService>(sp => new FlowService(
    sp.GetServices<ProcessDefinition>(),  // Toutes les définitions et versions
    sp.GetRequiredService<IProcessRepository>(),
    sp.GetServices<INodeHandler>()
));
```

### Sans persistance (usage direct du moteur)

Voir `Examples/Example.cs` pour un exemple simple sans base de données.

### Avec persistance Oracle

Voir `Examples/ExampleWithOracle.cs` pour un exemple complet avec Oracle.

### Avec historique

Voir `Examples/ExampleWithHistory.cs` pour voir comment analyser l'historique d'exécution.

## Migration de version

La librairie permet de migrer les instances en attente vers une nouvelle version de définition de processus.

### Conditions

Seules les instances dans un état d'attente peuvent être migrées :
- `WaitingInteraction`
- `WaitingSignal`
- `WaitingDate`

### Principe

La migration mappe les nœuds par **nom** (pas par ID interne). Si un nœud conserve le même nom entre les versions, il est mappé automatiquement. Sinon, un mapping explicite est requis.

### Exemple

#### Fichier JSON de migration

```json
{
    "fromVersion": "1.0",
    "toVersion": "2.0",
    "nodeMappings": {
        "Review": "DetailedReview"
    },
    "variableTransforms": [
        { "type": "set", "name": "MigratedFromV1", "value": true },
        { "type": "rename", "name": "OldStatus", "newName": "ReviewStatus" },
        { "type": "remove", "name": "DeprecatedFlag" }
    ]
}
```

#### Chargement et application

```csharp
using SimpleBPM.Migration;

// Charger depuis JSON
var migration = ProcessMigrationLoader.FromJsonFile("migrations/v1_to_v2.json");

// Ou depuis une chaîne JSON
var migration = ProcessMigrationLoader.FromJson(jsonString);

// Appliquer via IFlowService
var result = await flowService.MigrateAsync("order-123", v2Definition, migration);

if (result.Success)
    Console.WriteLine($"Migré de {result.PreviousVersion} vers {result.NewVersion}");
else
    Console.WriteLine($"Échec : {result.ErrorMessage}");
```

#### API fluide (alternative au JSON)

```csharp
var migration = new ProcessMigration("1.0", "2.0")
    .MapNode("Review", "DetailedReview")
    .SetVariable("MigratedFromV1", true)
    .RenameVariable("OldStatus", "ReviewStatus")
    .RemoveVariable("DeprecatedFlag");
```

### Transformations de variables

| Type | Description | Propriétés JSON |
|------|-------------|-----------------|
| `set` | Définir une variable | `name`, `value` |
| `rename` | Renommer une variable | `name`, `newName` |
| `remove` | Supprimer une variable | `name` |

## Interfaces client (Abstractions)

La librairie définit deux interfaces dans `Abstractions/` que l'application client doit implémenter :

### ICommandExecutor

Exécute les commandes métier et évalue les décisions. Implémenté côté client.

```csharp
public interface ICommandExecutor
{
    Task ExecuteCommandAsync(string commandName, string processId, string? aggregateId);
    Task<string> EvaluateDecisionAsync(string decisionName, string processId, string? aggregateId);
}
```

### IGestionTache

Gestion de tâches pour les nœuds interactifs. Optionnel — si fourni, le handler interactif crée automatiquement une tâche à l'entrée du nœud et la ferme à la sortie.

```csharp
public interface IGestionTache
{
    Task CreerTacheAsync(string processId, string? aggregateId, string definitionName, string nodeName);
    Task FermerTacheAsync(string processId, string? aggregateId, string definitionName, string nodeName);
}
```

Le cycle de vie est :
1. **Entrée** dans un nœud interactif → `CreerTacheAsync` (l'utilisateur voit la tâche)
2. **Sortie** du nœud (via `ContinueAsync`) → `FermerTacheAsync` (la tâche est fermée)

## Architecture

- Le `FlowService` gère l'ensemble des définitions de processus et de leurs versions
- Le client interagit via `IFlowService` en spécifiant le nom de la définition au démarrage
- L'instance stocke le nom et la version de la définition pour retrouver le bon processus
- Le processus s'exécute nœud par nœud jusqu'à rencontrer un nœud d'arrêt ou la fin naturelle
- Les nœuds métier appellent des commandes via `ICommandExecutor`
- Les décisions sont évaluées via `ICommandExecutor.EvaluateDecisionAsync`
- Chaque type de nœud a un handler dédié injecté avec ses propres dépendances
- Les handlers par défaut (Interactive, WaitForSignal, WaitUntilDate, SubProcess) sont auto-enregistrés
- Les variables d'instance (`Variables`) stockent l'état partagé entre les nœuds
- L'instance est automatiquement sauvegardée/mise à jour dans Oracle après chaque exécution
- Les sous-processus peuvent être imbriqués et sont gérés de manière transparente

## Structure du projet

```
SimpleBPM/
├── Abstractions/      # Interfaces client (ICommandExecutor, IGestionTache)
├── Definition/        # Fluent Builder et chargeur JSON
├── Examples/          # Exemples d'utilisation
├── Handlers/          # Handlers par type de nœud (logique d'exécution)
├── Migration/         # Migration de version (ProcessMigration, Runner, Result)
├── Nodes/             # Définitions des nœuds (données seulement)
├── Persistence/       # Repository Oracle et configuration
├── IFlowService.cs    # Interface client (compatible BPM existant)
├── FlowService.cs     # Implémentation (multi-définitions, multi-versions)
├── FlowEngine.cs      # Moteur d'exécution (multi-définitions, multi-versions)
├── Processus.cs       # Vue externe d'une instance de processus
├── InstanceNode.cs    # Vue externe d'une instance de nœud
├── ProcessInstance.cs # Instance de processus interne
├── ProcessNode.cs     # Classe de base des nœuds
└── ProcessDefinition.cs # Définition d'un processus (nom + version)
```

## Script SQL

Un script SQL manuel est disponible dans `schema.sql` pour créer les tables manuellement si nécessaire.
