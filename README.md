# SimpleBPM - Librairie BPM simple en C#

Une librairie légère pour gérer des processus métier (BPM) avec différents types de nœuds et persistance Oracle.

## Types de nœuds

- **BusinessNode** : Exécute une commande ou query métier
- **DecisionNode** : Permet de router vers différents nœuds selon le résultat d'une query
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
    .Query("CheckInventory", "Vérifier le stock")
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
            "isQuery": true,
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
- **Transfert de données** : Données d'entrée (préfixe `SUB_INPUT_`) et de sortie (préfixe `OUTPUT_`)
- **Gestion d'état** : Sauvegarde automatique de l'état du sous-processus en cas d'arrêt
- **Reprise** : Capacité à reprendre un sous-processus après une pause

### Exemple

```csharp
// Créer un sous-processus
var subProcessDef = new ProcessDefinition("ValidationProcess");
// ... ajouter des nœuds au sous-processus

// Utiliser dans le processus principal
var subProcessNode = new SubProcessNode(subProcessDef)
{
    Name = "Validation complète",
    InheritAggregateId = true
};
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
services.AddSingleton<ICommandQueryExecutor, MyCommandQueryExecutor>();
services.AddSingleton<INodeHandler>(sp => new BusinessNodeHandler(sp.GetRequiredService<ICommandQueryExecutor>()));
services.AddSingleton<INodeHandler>(sp => new DecisionNodeHandler(sp.GetRequiredService<ICommandQueryExecutor>()));
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

Le client interagit avec la librairie via l'interface `IFlowService`, en passant l'ID du processus et/ou de l'agrégat.

```csharp
// Démarrer un processus
await flowService.StartAsync("order-123", aggregateId: "client-456", variables: new()
{
    ["SUB_INPUT_OrderAmount"] = 1500.00
});

// Continuer un processus en attente
await flowService.ContinueAsync("order-123");

// Envoyer un signal
await flowService.SignalAsync("order-123", "PaymentReceived");

// Vérifier le statut
var status = await flowService.GetStatusAsync("order-123");
```

### Avec Dependency Injection

```csharp
services.AddScoped<IFlowService>(sp => new FlowService(
    processDefinition,
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

## Architecture

- Le processus s'exécute nœud par nœud jusqu'à rencontrer un nœud d'arrêt ou la fin naturelle
- Les nœuds métier et décisionnels appellent des commandes/queries via leur nom et l'ID du processus/agrégat
- Le client interagit via `IFlowService` avec des ID de processus et d'agrégat
- Chaque type de nœud a un handler dédié injecté avec ses propres dépendances
- Les handlers par défaut (Interactive, WaitForSignal, WaitUntilDate, SubProcess) sont auto-enregistrés
- Les variables d'instance (`Variables`) stockent l'état partagé entre les nœuds
- L'instance est automatiquement sauvegardée/mise à jour dans Oracle après chaque exécution
- Les sous-processus peuvent être imbriqués et sont gérés de manière transparente

## Structure du projet

```
SimpleBPM/
├── Definition/        # Fluent Builder et chargeur JSON
├── Examples/          # Exemples d'utilisation
├── Handlers/          # Handlers par type de nœud (logique d'exécution)
├── Migration/         # Migration de version (ProcessMigration, Runner, Result)
├── Nodes/             # Définitions des nœuds (données seulement)
├── Persistence/       # Repository Oracle et configuration
├── IFlowService.cs    # Interface client
├── FlowService.cs     # Implémentation du service
├── ProcessEngine.cs   # Moteur d'exécution interne
├── ProcessInstance.cs # Instance de processus en cours
├── ProcessNode.cs     # Classe de base des nœuds
└── ProcessDefinition.cs # Définition d'un processus
```

## Script SQL

Un script SQL manuel est disponible dans `schema.sql` pour créer les tables manuellement si nécessaire.
