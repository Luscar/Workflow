# SimpleBPM — Guide d'intégration client

Ce guide explique comment intégrer SimpleBPM dans une application .NET.

## Installation rapide

### 1. Définir les handlers métier

Implémentez `IBpmCommandHandler` pour chaque commande et `IBpmQueryHandler` pour chaque décision :

```csharp
using SimpleBPM.Abstractions;

// Handler de commande
public class ValidateOrderHandler : IBpmCommandHandler
{
    public string CommandName => "ValidateOrder";

    public Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        // Logique métier de validation
        return Task.CompletedTask;
    }
}

// Handler de décision (retourne le nom de la route)
public class ApprovalDecisionHandler : IBpmQueryHandler
{
    public string QueryName => "ApprovalDecision";

    public Task<string> HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        // Retourner le nom de la route suivante
        return Task.FromResult("approved");
    }
}
```

### 2. Définir un processus

#### Avec le Fluent Builder

```csharp
using SimpleBPM.Definition;

var process = ProcessBuilder.Create("OrderProcess", version: "1.0")
    .Business("ValidateOrder", "Valider la commande")
    .Business("CheckInventory", "Vérifier le stock")
        .WithParameter("WarehouseId", "WH-001")
    .Decision("DecideApproval", "Décision d'approbation", routes => routes
        .When("approved", "ProcessApproved")
        .When("rejected", "RejectOrder"))
    .Business("ProcessApproved", "Traitement approuvé")
        .Then("WaitPayment")
    .Business("RejectOrder", "Rejet commande")
        .End("OrderRejected", "Commande rejetée")
    .WaitForSignal("WaitPayment", "Attente paiement", "PaymentReceived")
    .Interactive("ManualReview", "Revue manuelle")
    .Build();
```

#### Depuis un fichier JSON

```csharp
var process = ProcessJsonLoader.FromJsonFile("processes/order.json");
```

### 3. Configurer le DI

#### Microsoft DI

```csharp
using SimpleBPM.Localisation;

services.AddSimpleBPM(options =>
{
    options.ScanHandlers(Assembly.GetExecutingAssembly());
    options.AddProcess(process);
    options.UseOracle("BPM");          // Optionnel — mémoire par défaut
    options.UseTaskManager<MyTaskManager>(); // Optionnel
});

// Connexion Oracle (si UseOracle est appelé)
services.AddScoped<IDbConnection>(sp =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return conn;
});
```

#### Autofac

```csharp
using SimpleBPM.Localisation;

builder.RegisterModule(new SimpleBPMAutofacModule(m =>
{
    m.ScanHandlers(Assembly.GetExecutingAssembly());
    m.AddProcess(process);
    m.UseOracle("BPM");
    m.UseTaskManager<MyTaskManager>();
}));
```

### 4. Utiliser IFlowService

```csharp
// Démarrer un processus
long processId = await flowService.CreateProcessInstanceAsync("OrderProcess", new()
{
    ["OrderAmount"] = 1500.00,
    ["CustomerId"] = "C-001"
});

// Obtenir l'état courant
var proc = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Statut: {proc.Status}, Nœud: {proc.CurrentNodeId}");

// Terminer une étape interactive
await flowService.TerminerEtapeEnCoursAsync(processId, new()
{
    ["ReviewDecision"] = "approved"
});

// Envoyer un signal
await flowService.EnvoyerSignalAsync(processId, "PaymentReceived");

// Rechercher par variable
var resultats = await flowService.RechercherParVariableAsync(new List<FiltreVariable>
{
    new("CustomerId", "C-001", OperateurFiltre.Egal, TypeDonnee.Texte)
});
```

---

## Types de nœuds

### BusinessNode

Exécute une commande métier via `IBpmCommandHandler`.

```csharp
.Business("SendEmail", "Envoyer courriel")
    .WithParameter("Template", "OrderConfirmation")
```

### DecisionNode

Route le flux selon des conditions sur les variables ou via `IBpmQueryHandler`.

```csharp
// Conditions sur variables (évaluées dans l'ordre)
var node = new DecisionNode { Name = "CheckAmount" };
node.AddCondition("Amount", 10000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "VIPPath")
    .AddCondition("Amount", 1000, OperateurFiltre.Superieur, TypeDonnee.Nombre, "StandardPath")
    .SetNoeudParDefaut("DefaultPath");

// Ou via query externe (IBpmQueryHandler)
.Decision("EligibilityCheck", "Vérification éligibilité", routes => routes
    .When("eligible", "ProcessApplication")
    .When("rejected", "RejectApplication"))
```

### InteractiveNode

Suspend le processus (`WaitingInteraction`) et attend `TerminerEtapeEnCoursAsync`.

```csharp
.Interactive("UnderwriterReview", "Revue souscripteur")
```

### WaitUntilDateNode

Suspend jusqu'à une date calculée dynamiquement via une query.

```csharp
.WaitUntilDate("WaitDeadline", "Attente échéance", "GetDeadlineDate")
```

### WaitForSignalNode

Suspend jusqu'à la réception d'un signal nommé.

```csharp
.WaitForSignal("WaitPayment", "Attente paiement", "PaymentReceived")
// Puis : await flowService.EnvoyerSignalAsync(processId, "PaymentReceived");
```

### SubProcessNode

Lance un sous-processus et attend sa complétion avant de continuer.

```csharp
.SubProcess("ValidationStep", validationDefinition,
    inputMapping: new() { ["OrderAmount"] = "Amount" },
    outputMapping: new() { ["ValidationResult"] = "IsValid" })
```

### EndNode

Termine explicitement une branche — marque l'instance comme `Completed`.

```csharp
.Business("RejectOrder", "Rejet")
    .End("OrderRejected", "Commande rejetée")
```

---

## Persistance des définitions

Les définitions peuvent être sauvegardées en banque pour un chargement dynamique sans redéploiement.

```csharp
// Sauvegarder
await flowService.SaveDefinitionAsync(processDefinition);

// Lister toutes les définitions (banque + mémoire)
var definitions = await flowService.GetDefinitionsAsync();
```

Le moteur charge automatiquement une définition depuis la banque si elle est absente en mémoire.

---

## Migration de version

Migre des instances **en attente** (`WaitingInteraction`, `WaitingSignal`, `WaitingDate`) vers une nouvelle version.

```csharp
using SimpleBPM.Migration;

var migration = new ProcessMigration("1.0", "2.0")
    .MapNode("Review", "DetailedReview")    // nœud renommé
    .SetVariable("MigratedAt", DateTime.UtcNow)
    .RenameVariable("OldStatus", "ReviewStatus")
    .RemoveVariable("DeprecatedFlag");

var result = await flowService.MigrateAsync(processId, v2Definition, migration);

if (result.Success)
    Console.WriteLine($"Migré de {result.PreviousVersion} vers {result.NewVersion}");
else
    Console.WriteLine($"Échec : {result.ErrorMessage}");
```

Depuis un fichier JSON :

```csharp
var migration = ProcessMigrationLoader.FromJsonFile("migrations/v1_to_v2.json");
```

---

## Gestion de tâches (IGestionTache)

Interface optionnelle pour la création/fermeture de tâches sur les nœuds interactifs.

```csharp
public class MyTaskManager : IGestionTache
{
    public Task CreerTacheAsync(string processId, string? aggregateId,
        string definitionName, string nodeName)
    {
        // Créer une tâche dans votre système (ex. base de données, ticketing)
        return Task.CompletedTask;
    }

    public Task FermerTacheAsync(string processId, string? aggregateId,
        string definitionName, string nodeName)
    {
        // Fermer la tâche correspondante
        return Task.CompletedTask;
    }
}
```

Enregistrement :

```csharp
options.UseTaskManager<MyTaskManager>();
// ou : services.AddSingleton<IGestionTache, MyTaskManager>();
```

---

## Surveillance (IProcessMonitor)

Interface en lecture seule — ne nécessite pas `IBpmMediateur`.

```csharp
// Toutes les instances racines
var instances = await monitor.GetRootInstancesAsync();

// Filtrer par statut
var enAttente = await monitor.GetInstancesByStatusAsync(ProcessStatus.WaitingInteraction);

// Historique d'un processus
var history = await monitor.GetExecutionHistoryAsync(processId);

// Résumé par statut
var summary = await monitor.GetStatusSummaryAsync();

// Définitions disponibles (banque + mémoire)
var definitions = await monitor.GetDefinitionsAsync();
```

Configuration minimale pour un client de monitoring uniquement (ex. Blazor dashboard) :

```csharp
// Microsoft DI
services.AddProcessMonitoring();

// Autofac
builder.RegisterModule(new ProcessMonitoringAutofacModule());
```

---

## Recherche par variable

```csharp
var filtres = new List<FiltreVariable>
{
    new("CustomerId", "C-001", OperateurFiltre.Egal, TypeDonnee.Texte),
    new("Amount", 5000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre),
    new("CreatedAt", DateTime.Today.AddDays(-30), OperateurFiltre.Superieur, TypeDonnee.Date)
};

var resultats = await flowService.RechercherParVariableAsync(filtres);
```

### Opérateurs disponibles

| Opérateur | Description |
|-----------|-------------|
| `Egal` | Égalité stricte |
| `Different` | Différence |
| `Superieur` | Strictement supérieur |
| `SuperieurOuEgal` | Supérieur ou égal |
| `Inferieur` | Strictement inférieur |
| `InferieurOuEgal` | Inférieur ou égal |
| `Contient` | Contient la sous-chaîne (texte) |
| `CommencePar` | Commence par (texte) |
| `FinitPar` | Finit par (texte) |

### Types de données

`Texte` · `Nombre` · `Date` · `Booleen`

---

## Statuts de processus

| Statut | Description |
|--------|-------------|
| `Running` | Exécution en cours |
| `WaitingInteraction` | Attente d'une action utilisateur |
| `WaitingDate` | Attente d'une date cible |
| `WaitingSignal` | Attente d'un signal nommé |
| `Completed` | Processus terminé avec succès |
| `Failed` | Processus en erreur |

---

## Exemple complet

Voir `SimpleBPM.ExampleClient/` pour un exemple d'intégration complet avec un workflow d'approbation de prêt bancaire (validation, vérifications, décision crédit, revue manuelle, signature, décaissement).
