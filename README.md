# SimpleBPM - Librairie BPM simple en C#

Une librairie légère pour gérer des processus métier (BPM) avec différents types de nœuds et persistance Oracle.

## Types de nœuds

- **BusinessNode** : Exécute une commande métier
- **DecisionNode** : Permet de router vers différents nœuds selon des conditions sur les variables ou le résultat d'une évaluation externe
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
        .WithParameter("WarehouseId", "WH-001")
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
            "parameters": { "WarehouseId": "WH-001" },
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

// Charger depuis une chaîne JSON
var process = ProcessJsonLoader.FromJson(json);

// Charger depuis un fichier
var processFromFile = ProcessJsonLoader.FromJsonFile("process.json");

// Exporter en JSON
var json = ProcessJsonLoader.ToJson(process);
```

Voir `Examples/ExampleWithBuilder.cs` pour un exemple complet.

## Nœuds de décision

Les nœuds de décision permettent de router le flux vers différents nœuds selon des conditions.

### Deux modes de fonctionnement

1. **Conditions sur variables** (recommandé) : Évalue directement les variables du processus
2. **Query externe** : Délègue l'évaluation à `ICommandExecutor.EvaluateDecisionAsync`

### Opérateurs disponibles

| Opérateur | Description | Types supportés |
|-----------|-------------|-----------------|
| `Egal` | Égalité | Tous |
| `Different` | Différence | Tous |
| `Superieur` | Strictement supérieur | Nombre, Date, Texte |
| `SuperieurOuEgal` | Supérieur ou égal | Nombre, Date, Texte |
| `Inferieur` | Strictement inférieur | Nombre, Date, Texte |
| `InferieurOuEgal` | Inférieur ou égal | Nombre, Date, Texte |
| `Contient` | Contient la sous-chaîne | Texte |
| `CommencePar` | Commence par | Texte |
| `FinitPar` | Finit par | Texte |

### Types de données

- `Texte` : Comparaison de chaînes
- `Nombre` : Comparaison numérique (int, long, double, decimal)
- `Date` : Comparaison de dates
- `Booleen` : Comparaison booléenne

### Exemple avec conditions

```csharp
var decisionNode = new DecisionNode()
{
    Name = "VerifierMontant",
    DisplayName = "Vérifier le montant"
};

decisionNode
    .AddCondition("Montant", 10000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "TraitementVIP")
    .AddCondition("Montant", 1000, OperateurFiltre.Superieur, TypeDonnee.Nombre, "TraitementPrioritaire")
    .AddCondition("TypeClient", "Premium", OperateurFiltre.Egal, TypeDonnee.Texte, "TraitementPremium")
    .SetNoeudParDefaut("TraitementStandard");
```

Les conditions sont évaluées dans l'ordre. La première condition vraie détermine le nœud suivant. Si aucune condition ne correspond, le `NoeudParDefaut` est utilisé.

### Exemple avec query externe

```csharp
var decisionNode = new DecisionNode("EvaluerEligibilite")
    .AddRoute("eligible", "TraiterDemande")
    .AddRoute("non_eligible", "RejeterDemande");
```

## Paramètres de nœuds

Chaque nœud peut porter des paramètres statiques (`Dictionary<string, object>`) définis à la conception. Ces paramètres sont transmis automatiquement à `ICommandExecutor` lors de l'exécution.

### Avec le Fluent Builder

```csharp
var process = ProcessBuilder.Create("OrderProcess")
    .Business("SendEmail", "Envoyer courriel")
        .WithParameter("Template", "OrderConfirmation")
        .WithParameter("Priority", "High")
    .Business("Archive", "Archiver")
        .WithParameters(new() { ["RetentionDays"] = 90, ["Compress"] = true })
    .Build();
```

### Avec JSON

```json
{
    "name": "SendEmail",
    "type": "Business",
    "command": "SendEmail",
    "parameters": {
        "Template": "OrderConfirmation",
        "Priority": "High"
    },
    "next": ["Archive"]
}
```

### Réception côté client

Les paramètres sont passés en dernier argument de `ICommandExecutor` :

```csharp
public class MyCommandExecutor : ICommandExecutor
{
    public Task ExecuteCommandAsync(string commandName, long processId, string? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var template = parameters?["Template"]?.ToString();
        // ...
    }
}
```

## Sous-processus

Les sous-processus permettent de décomposer des processus complexes en sous-unités réutilisables.

### Caractéristiques

- **Héritage d'agrégat** : Le sous-processus peut hériter de l'ID d'agrégat du processus parent
- **Mapping explicite** : Les variables sont transférées via `InputMapping` (parent → sous-processus) et `OutputMapping` (sous-processus → parent)
- **Gestion d'état** : Sauvegarde automatique de l'état du sous-processus en cas d'arrêt
- **Reprise** : Capacité à reprendre un sous-processus après une pause
- **Suivi dédié** : Les sous-processus sont liés au parent via `ParentProcessId` et récupérables via `ObtenirEnfants`

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
var oracleConfig = new OracleConfiguration("ABC"); // Préfixe de 3 à 10 lettres

// Connexion gérée par le client
using var connection = new OracleConnection("User Id=myuser;Password=mypass;Data Source=localhost:1521/XEPDB1");
connection.Open();

var repository = new OracleProcessRepository(oracleConfig, connection);
await repository.InitializeDatabaseAsync();
```

### Avec Dependency Injection

#### Avec injection de handlers (recommandé)

```csharp
using System.Reflection;
using SimpleBPM.Localisation;

// Program.cs / Startup.cs

// 1. Auto-découvrir et enregistrer les ICommandHandler / IQueryHandler
services.AddCommandHandlers(Assembly.GetExecutingAssembly());

// 2. Enregistrer les services optionnels
services.AddSingleton<IGestionTache, MyGestionTache>(); // Optionnel

// 3. Enregistrer les définitions de processus
services.AddSingleton(orderProcessDefinition);
services.AddSingleton(invoiceProcessDefinition);

// 4. Enregistrer SimpleBPM (Oracle avec connexion, handlers, FlowService)
services.AddSimpleBPM(tablePrefix: "BPM", connectionFactory: sp =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return conn;
});
```

#### Avec ICommandExecutor direct

```csharp
using SimpleBPM.Localisation;

// Program.cs / Startup.cs

// 1. Enregistrer l'implémentation ICommandExecutor (requis)
services.AddSingleton<ICommandExecutor, MyCommandExecutor>();
services.AddSingleton<IGestionTache, MyGestionTache>(); // Optionnel

// 2. Enregistrer les définitions de processus
services.AddSingleton(orderProcessDefinition);
services.AddSingleton(invoiceProcessDefinition);

// 3. Enregistrer SimpleBPM (Oracle avec connexion, handlers, FlowService)
services.AddSimpleBPM(tablePrefix: "BPM", connectionFactory: sp =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return conn;
});
```

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
// Créer une instance de processus (retourne un long)
long processId = await flowService.CreateProcessInstance("OrderProcess", new()
{
    ["OrderAmount"] = 1500.00
});

// Obtenir un processus
var processus = await flowService.ObtenirAsync(processId);

// Terminer une étape (nœud interactif) avec du contenu
long nodeInstanceId = /* ... */;
await flowService.TerminerEtape(nodeInstanceId, new Dictionary<string, object>
{
    ["Decision"] = "approved"
});

// Obtenir les signaux en attente
var signaux = await flowService.ObtenirSignauxEnAttente(processId);

// Rechercher par variable avec filtres
var filtres = new List<FiltreVariable>
{
    new("OrderAmount", 1000, OperateurFiltre.Superieur, TypeDonnee.Nombre),
    new("Status", "Active", OperateurFiltre.Egal, TypeDonnee.Texte)
};
var resultats = await flowService.RechercherParVariable(filtres);

// Obtenir les sous-processus enfants
var enfants = await flowService.ObtenirEnfants(processId);

// Obtenir un nœud d'instance
var noeud = await flowService.Obtenir(nodeInstanceId);
```

### Recherche par variable

La méthode `RechercherParVariable` permet de rechercher des instances de processus selon leurs variables avec des opérateurs de comparaison.

```csharp
// Recherche simple (égalité)
var filtres = new List<FiltreVariable>
{
    new("ClientId", "12345", OperateurFiltre.Egal, TypeDonnee.Texte)
};

// Recherche avec plusieurs conditions (AND)
var filtres = new List<FiltreVariable>
{
    new("Montant", 5000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre),
    new("DateCreation", DateTime.Today.AddDays(-30), OperateurFiltre.Superieur, TypeDonnee.Date),
    new("Statut", "EnCours", OperateurFiltre.Egal, TypeDonnee.Texte)
};

var resultats = await flowService.RechercherParVariable(filtres);
```

Les mêmes opérateurs et types de données que pour les nœuds de décision sont disponibles (voir section "Nœuds de décision").

### Sans persistance (usage direct du moteur)

Voir `Examples/Example.cs` pour un exemple simple sans base de données.

### Avec persistance Oracle

Voir `Examples/ExampleWithOracle.cs` pour un exemple complet avec Oracle, incluant l'analyse de l'historique d'exécution.

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

La librairie définit plusieurs interfaces dans `Abstractions/` que l'application client peut implémenter.

### ICommandHandler / IQueryHandler (recommandé)

Approche par **injection de handlers** : chaque commande ou décision est un handler individuel, découvert et enregistré automatiquement par réflexion.

```csharp
public interface ICommandHandler
{
    string CommandName { get; }
    Task HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
}

public interface IQueryHandler
{
    string QueryName { get; }
    Task<string> HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
}
```

Le client implémente un handler par commande/décision :

```csharp
public class ValidateApplicationHandler : ICommandHandler
{
    public string CommandName => "ValidateApplication";

    public Task HandleAsync(long processId, string? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        // Logique métier de validation
        return Task.CompletedTask;
    }
}

public class CreditDecisionHandler : IQueryHandler
{
    public string QueryName => "CreditDecision";

    public Task<string> HandleAsync(long processId, string? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        // Logique de décision, retourne le nom de la route
        return Task.FromResult("approved");
    }
}
```

Les handlers sont découverts automatiquement via `AddCommandHandlers()` (voir section [Injection de handlers](#injection-de-handlers)).

### ICommandExecutor (approche directe)

Alternative à l'injection de handlers : une seule classe qui gère toutes les commandes et décisions. Utile pour les cas simples ou quand une logique centralisée est préférée.

```csharp
public interface ICommandExecutor
{
    Task ExecuteCommandAsync(string commandName, long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
    Task<string> EvaluateDecisionAsync(string decisionName, long processId, string? aggregateId, Dictionary<string, object>? parameters = null);
}
```

> **Note** : Les deux approches sont mutuellement exclusives. `AddCommandHandlers()` enregistre automatiquement un `CommandHandlerExecutor` comme `ICommandExecutor`, qui dispatche vers les handlers individuels. Si vous utilisez l'injection de handlers, vous n'avez pas besoin d'implémenter `ICommandExecutor` directement.

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

## Injection de handlers

L'injection de handlers permet de découper la logique métier en handlers individuels (`ICommandHandler` pour les commandes, `IQueryHandler` pour les décisions) qui sont découverts et enregistrés automatiquement via réflexion.

### Fonctionnement

1. Le client implémente un `ICommandHandler` par commande métier et un `IQueryHandler` par décision
2. `AddCommandHandlers(Assembly.GetExecutingAssembly())` scanne les assemblies et enregistre tous les handlers trouvés
3. Un `CommandHandlerExecutor` est automatiquement enregistré comme `ICommandExecutor`
4. Au runtime, les commandes sont dispatchées vers le bon handler selon le `CommandName` / `QueryName`

### Enregistrement

```csharp
using System.Reflection;
using SimpleBPM.Localisation;

// Auto-découverte et enregistrement de tous les ICommandHandler / IQueryHandler
services.AddCommandHandlers(Assembly.GetExecutingAssembly());

// Optionnel : scanner plusieurs assemblies
services.AddCommandHandlers(
    Assembly.GetExecutingAssembly(),
    typeof(SharedHandlers.SomeHandler).Assembly
);
```

### Dispatch

Le `CommandHandlerExecutor` maintient un dictionnaire interne indexé par `CommandName` / `QueryName` pour un dispatch en O(1) :

- `ExecuteCommandAsync("ValidateApplication", ...)` → `ValidateApplicationHandler.HandleAsync(...)`
- `EvaluateDecisionAsync("CreditDecision", ...)` → `CreditDecisionHandler.HandleAsync(...)`

Si aucun handler n'est enregistré pour une commande ou décision donnée, une `InvalidOperationException` est levée.

### Exemple complet

Voir `SimpleBPM.ExampleClient/` pour un projet client complet utilisant l'injection de handlers avec un workflow d'approbation de prêt.

## Architecture

- Le `FlowService` gère l'ensemble des définitions de processus et de leurs versions
- Le client interagit via `IFlowService` en spécifiant le nom de la définition au démarrage
- L'instance stocke le nom et la version de la définition pour retrouver le bon processus
- Le processus s'exécute nœud par nœud jusqu'à rencontrer un nœud d'arrêt ou la fin naturelle
- Les nœuds métier appellent des commandes via `ICommandExecutor`
- Les décisions sont évaluées via `ICommandExecutor.EvaluateDecisionAsync`
- Chaque type de nœud a un handler dédié (`INodeHandler`) injecté avec ses propres dépendances
- Les handlers par défaut (Interactive, WaitForSignal, WaitUntilDate, SubProcess) sont auto-enregistrés
- **Injection de handlers** : les commandes métier et décisions peuvent être implémentées comme des handlers individuels (`ICommandHandler` / `IQueryHandler`), découverts automatiquement via `AddCommandHandlers()` et dispatchés par `CommandHandlerExecutor`
- Les variables d'instance (`Variables`) stockent l'état partagé entre les nœuds
- L'instance est automatiquement sauvegardée/mise à jour dans Oracle après chaque exécution
- Les sous-processus peuvent être imbriqués et sont gérés de manière transparente

## Structure du projet

```
SimpleBPM.sln
├── SimpleBPM/                # Projet principal
│   ├── Abstractions/         # Interfaces client (ICommandHandler, IQueryHandler, ICommandExecutor, IGestionTache)
│   ├── Definition/           # Fluent Builder et chargeur JSON
│   ├── Examples/             # Exemples d'utilisation
│   ├── Handlers/             # Handlers par type de nœud (logique d'exécution)
│   ├── Localisation/         # Enregistrement DI (extension AddSimpleBPM)
│   ├── Migration/            # Migration de version (ProcessMigration, Runner, Result)
│   ├── Nodes/                # Définitions des nœuds (données seulement)
│   ├── Persistence/          # Repository Oracle et configuration
│   ├── CommandHandlerExecutor.cs # Dispatcher vers ICommandHandler/IQueryHandler
│   ├── ConditionDecision.cs  # Condition pour nœuds de décision avec opérateurs
│   ├── FiltreVariable.cs     # Filtre pour recherche par variable avec opérateurs
│   ├── FlowEngine.cs         # Moteur d'exécution (multi-définitions, multi-versions)
│   ├── FlowService.cs        # Implémentation (multi-définitions, multi-versions)
│   ├── IFlowService.cs       # Interface client (compatible BPM existant)
│   ├── ProcessDefinition.cs  # Définition d'un processus (nom + version)
│   ├── ProcessInstance.cs    # Instance de processus interne
│   ├── ProcessNode.cs        # Classe de base des nœuds
│   ├── Processus.cs          # Vue externe d'une instance de processus
│   └── InstanceNode.cs       # Vue externe d'une instance de nœud
├── SimpleBPM.ExampleClient/   # Exemple client complet (injection de handlers)
│   ├── CommandHandlers/       # Implémentations ICommandHandler
│   ├── QueryHandlers/         # Implémentations IQueryHandler
│   └── Program.cs             # Point d'entrée avec setup DI
├── SimpleBPM.Tests/           # Tests unitaires (xUnit)
│   ├── ConditionDecisionTests.cs
│   ├── FiltreVariableTests.cs
│   ├── FlowEngineTests.cs
│   ├── HandlersTests.cs
│   ├── NodeExecutionHistoryTests.cs
│   ├── OracleConfigurationTests.cs
│   ├── ProcessBuilderTests.cs
│   ├── ProcessDefinitionTests.cs
│   ├── ProcessInstanceTests.cs
│   ├── ProcessJsonLoaderTests.cs
│   ├── ProcessNodeTests.cs
│   └── ProcessusTests.cs
└── schema.sql                 # Script SQL Oracle (création manuelle des tables)
```

## Tests

Le projet `SimpleBPM.Tests` contient des tests unitaires xUnit couvrant l'ensemble des composants :

- **FlowEngineTests** : Exécution, continuation, signaux, historique, cas d'erreur
- **ProcessBuilderTests** : API fluide, chaînage, décisions, sous-processus, paramètres
- **ProcessJsonLoaderTests** : Sérialisation aller-retour JSON, tous les types de nœuds, paramètres
- **ProcessDefinitionTests** : Ajout de nœuds, versions, nœud de départ
- **ProcessInstanceTests** : Statut, variables, durée, historique
- **ProcessNodeTests** : Types de nœuds, paramètres, NextNodeIds
- **ProcessusTests** : Vue externe d'une instance
- **ConditionDecisionTests** : Opérateurs, types de données, évaluation des conditions
- **FiltreVariableTests** : Filtres de recherche, opérateurs, types
- **HandlersTests** : Tous les handlers (Business, Decision, Interactive, WaitForSignal, WaitUntilDate, SubProcess)
- **NodeExecutionHistoryTests** : Historique d'exécution, durée
- **OracleConfigurationTests** : Validation du préfixe de tables

## Script SQL

Un script SQL manuel est disponible dans `schema.sql` pour créer les tables manuellement si nécessaire.
