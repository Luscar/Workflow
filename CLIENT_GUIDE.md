# SimpleBPM — Guide d'intégration client

Ce guide explique comment intégrer SimpleBPM dans une application cliente de zéro.
Il couvre la définition de processus avec le builder fluide, l'implémentation des handlers métier,
la configuration de l'injection de dépendances et l'exploitation des processus à l'exécution.

---

## Table des matières

1. [Prérequis](#1-prérequis)
2. [Référence de projet](#2-référence-de-projet)
3. [Définir un processus — le Fluent Builder](#3-définir-un-processus--le-fluent-builder)
   - [Bases du builder](#31-bases-du-builder)
   - [BusinessNode](#32-businessnode)
   - [DecisionNode](#33-decisionnode)
   - [InteractiveNode](#34-interactivenode)
   - [WaitForSignalNode](#35-waitforsignalnode)
   - [WaitUntilDateNode](#36-waituntildatenode)
   - [SubProcessNode](#37-subprocessnode)
   - [EndNode](#38-endnode)
   - [Paramètres de nœud](#39-paramètres-de-nœud)
   - [Commande OnEnter](#310-commande-onenter)
   - [Chaînage manuel avec Then et Break](#311-chaînage-manuel-avec-then-et-break)
4. [Définir un processus — JSON](#4-définir-un-processus--json)
5. [Implémenter la logique métier](#5-implémenter-la-logique-métier)
   - [IBpmCommandHandler](#51-ibpmcommandhandler)
   - [IBpmQueryHandler](#52-ibomqueryhandler)
   - [IBpmMediateur (approche directe)](#53-ibpmmediateur-approche-directe)
   - [IGestionTache (gestion de tâches optionnelle)](#54-igestiontache-gestion-de-tâches-optionnelle)
6. [Configuration de l'injection de dépendances](#6-configuration-de-linjection-de-dépendances)
   - [Microsoft DI — builder unifié](#61-microsoft-di--builder-unifié-recommandé)
   - [Microsoft DI — étape par étape](#62-microsoft-di--étape-par-étape)
   - [Module Autofac](#63-module-autofac)
   - [Persistance Oracle](#64-persistance-oracle)
   - [Banque de définitions](#65-banque-de-définitions)
7. [Exploiter les processus à l'exécution](#7-exploiter-les-processus-à-lexécution)
   - [Créer une instance de processus](#71-créer-une-instance-de-processus)
   - [Terminer une étape interactive](#72-terminer-une-étape-interactive)
   - [Envoyer un signal](#73-envoyer-un-signal)
   - [Consulter l'état d'un processus](#74-consulter-létat-dun-processus)
   - [Rechercher par variable](#75-rechercher-par-variable)
8. [Surveillance](#8-surveillance)
9. [Migration de version](#9-migration-de-version)
10. [Exemple complet — workflow d'approbation de prêt](#10-exemple-complet--workflow-dapprobation-de-prêt)

---

## 1. Prérequis

- .NET 8 ou version ultérieure
- Base de données Oracle (ou stockage en mémoire intégré pour les tests)
- Une référence au projet / package `SimpleBPM`

---

## 2. Référence de projet

```xml
<ProjectReference Include="../SimpleBPM/SimpleBPM.csproj" />
```

Pour la persistance Oracle, ajoutez également :

```xml
<PackageReference Include="Dapper" Version="2.*" />
<PackageReference Include="Oracle.ManagedDataAccess.Core" Version="23.*" />
```

---

## 3. Définir un processus — le Fluent Builder

`ProcessBuilder` est la méthode recommandée pour définir les processus. Il offre une validation à la compilation, l'IntelliSense et le chaînage automatique des nœuds.

### 3.1 Bases du builder

```csharp
using SimpleBPM;
using SimpleBPM.Definition;

ProcessDefinition processus = ProcessBuilder.Create("MonProcessus", "1.0")
    .Business("Etape1", "Première étape")
    .Business("Etape2", "Deuxième étape")
    .Build();
```

**Règle de chaînage automatique** : chaque nœud est automatiquement lié au précédent, *sauf si* :
- Le nœud précédent est un `DecisionNode` (il gère ses propres routes), ou
- Le nœud précédent a déjà un successeur explicite défini via `.Then()`, ou
- Un `.Break()` a été appelé pour rompre la chaîne.

`Build()` valide que tout `NextNodeId` et toute route de décision référencent un nœud qui existe réellement, en levant une `InvalidOperationException` dans le cas contraire.

---

### 3.2 BusinessNode

Un `BusinessNode` exécute une commande nommée en appelant `IBpmMediateur.ExecuteCommandAsync`.

```csharp
ProcessBuilder.Create("ProcessusCommande")
    .Business("ValiderCommande", "Valider la commande")
    .Business("EnvoyerConfirmation", "Envoyer l'e-mail de confirmation")
    .Build();
```

La chaîne passée en premier argument est *à la fois* le nom du nœud *et* le nom de la commande dispatchée vers `IBpmCommandHandler`.

Si la commande lève une exception, le nœud marque le processus comme `Failed` avec le message de l'exception.

---

### 3.3 DecisionNode

Un `DecisionNode` route le processus vers différentes branches. Il supporte deux modes.

#### Mode A — Requête externe (via handler)

```csharp
ProcessBuilder.Create("ProcessusPret")
    .Business("VerifierCredit", "Vérifier le score de crédit")
    .Decision("DecisionCredit", "Routage crédit", routes => routes
        .When("approuve",  "CalculerConditions")
        .When("rejete",    "RejeterDemande"))
    .Business("CalculerConditions", "Calculer les conditions du prêt")
        .Then("Decaisser")
    .Business("RejeterDemande", "Envoyer la lettre de refus")
        .End("Rejete", "Demande rejetée")
    .Business("Decaisser", "Décaisser les fonds")
    .Build();
```

Les appels `.When(resultat, idNoeudCible)` sur `DecisionRouteBuilder` associent une *chaîne de résultat* à un *nom de nœud*. Cette chaîne est retournée par `IBpmQueryHandler.HandleAsync`.

#### Mode B — Conditions sur variables (sans handler)

Au lieu d'un handler de requête, vous pouvez évaluer directement les variables du processus — aucune classe de handler supplémentaire n'est nécessaire :

```csharp
var noeudDecision = new DecisionNode
{
    Name = "VerifierMontant",
    DisplayName = "Vérifier le montant de la commande"
};

noeudDecision
    .AddCondition("Montant", 10_000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre, "CircuitVIP")
    .AddCondition("Montant",  1_000, OperateurFiltre.Superieur,       TypeDonnee.Nombre, "CircuitStandard")
    .SetNoeudParDefaut("CircuitStandard");
```

**Opérateurs disponibles** (`OperateurFiltre`) :

| Opérateur | Signification | Types applicables |
|---|---|---|
| `Egal` | Égalité | Tous |
| `Different` | Différence | Tous |
| `Superieur` | Strictement supérieur | `Nombre`, `Date`, `Texte` |
| `SuperieurOuEgal` | Supérieur ou égal | `Nombre`, `Date`, `Texte` |
| `Inferieur` | Strictement inférieur | `Nombre`, `Date`, `Texte` |
| `InferieurOuEgal` | Inférieur ou égal | `Nombre`, `Date`, `Texte` |
| `Contient` | Contient la sous-chaîne | `Texte` |
| `CommencePar` | Commence par | `Texte` |
| `FinitPar` | Finit par | `Texte` |

**Types de données disponibles** (`TypeDonnee`) : `Texte`, `Nombre`, `Date`, `Booleen`

---

### 3.4 InteractiveNode

Un `InteractiveNode` met le processus en pause et attend qu'un humain appelle `TerminerEtapeAsync` ou `TerminerEtapeEnCoursAsync`.

```csharp
ProcessBuilder.Create("ProcessusApprobation")
    .Business("Préparer", "Préparer les documents")
    .Interactive("ApprobationManager", "Approbation du manager")
    .Business("Archiver", "Archiver les documents approuvés")
    .Build();
```

Quand le moteur atteint ce nœud :
1. `ProcessStatus` passe à `WaitingInteraction`.
2. `CurrentNodeId` est enregistré pour que le moteur sache où reprendre.
3. Si `IGestionTache` est enregistré, `CreerTacheAsync` est appelé (crée une tâche visible par les utilisateurs).
4. Si une `OnEnterCommand` est configurée (voir §3.10), elle est exécutée avant l'arrêt.

Quand l'utilisateur termine l'étape, appelez `TerminerEtapeEnCoursAsync` (ou `TerminerEtapeAsync`).
Le moteur appelle `OnLeaveAsync` → `FermerTacheAsync` (si `IGestionTache` est enregistré), puis reprend à partir du nœud suivant.

---

### 3.5 WaitForSignalNode

Un `WaitForSignalNode` met le processus en pause jusqu'à ce qu'un système externe envoie un signal nommé.

```csharp
ProcessBuilder.Create("ProcessusCommande")
    .Business("PasserCommande", "Passer la commande")
    .WaitForSignal("AttenteConfirmationPaiement", "Attente de la confirmation de paiement")
    .Business("ExpedierCommande", "Expédier la commande")
    .Build();
```

Le nom du nœud est aussi utilisé comme nom de signal attendu. Envoyez le signal via :

```csharp
await flowService.EnvoyerSignalAsync(processId, "AttenteConfirmationPaiement");
```

Le moteur ne reprend que si le nom du signal reçu correspond à `WaitForSignalNode.SignalName`.

---

### 3.6 WaitUntilDateNode

Un `WaitUntilDateNode` met le processus en pause jusqu'à une date/heure précise. Trois stratégies de résolution de date sont disponibles :

#### Date fixe

```csharp
.WaitUntilDate("AttenteEcheance", new DateTime(2025, 12, 31), "Attendre la fin d'année")
```

#### Date lue depuis une variable du processus

```csharp
// Le processus doit avoir une variable "DatePlanifiee" contenant un DateTime
.WaitUntilDate("AttentePlanifiee", "DatePlanifiee", "Attendre la date planifiée")
```

#### Date calculée dynamiquement (lambda)

```csharp
.WaitUntilDate("AttenteDelai",
    instance => instance.StartedAt.AddDays(7),
    "Attendre 7 jours après le démarrage")
```

#### Date retournée par un handler de requête

```csharp
.WaitUntilDateQuery("AttenteDynamique", "ObtenirDateTraitement",
    queryParameters: new() { ["Type"] = "express" },
    displayName: "Attendre la date de traitement")
```

Le planificateur chargé de reprendre les instances en attente de date est typiquement implémenté par le client (ex. un job de fond qui appelle `TerminerEtapeEnCoursAsync` lorsque la date est atteinte).

---

### 3.7 SubProcessNode

Un `SubProcessNode` délègue l'exécution à une définition de processus imbriquée. Les variables circulent entre parent et enfant via des mappings explicites.

#### Avec une ProcessDefinition existante

```csharp
var processusVerification = ProcessBuilder.Create("Verification")
    .Business("VerifierIdentite", "Vérifier l'identité")
    .Business("VerifierRevenus",  "Vérifier les revenus")
    .Build();

ProcessBuilder.Create("ProcessusPret")
    .Business("Demarrage", "Démarrer la demande")
    .SubProcess("Verification", processusVerification,
        inputMapping:  new() { ["IdDemandeur"] = "IdClient" },
        outputMapping: new() { ["ResultatVerification"] = "EstVerifie" },
        inheritAggregateId: true,
        displayName: "Vérification du demandeur")
    .Business("Continuer", "Continuer après vérification")
    .Build();
```

#### Avec un builder inline

```csharp
ProcessBuilder.Create("ProcessusPret")
    .Business("Demarrage", "Démarrer la demande")
    .SubProcess("Verification", sub => sub
        .Business("VerifierIdentite", "Vérifier l'identité")
        .Business("VerifierRevenus",  "Vérifier les revenus"),
        inputMapping:  new() { ["IdDemandeur"] = "IdClient" },
        outputMapping: new() { ["ResultatVerification"] = "EstVerifie" })
    .Business("Continuer", "Continuer après vérification")
    .Build();
```

**Règles de mapping** :

- `inputMapping`  — `{ "VarParent": "VarEnfant" }` copie `VarParent` du parent vers `VarEnfant` dans l'enfant avant l'exécution.
- `outputMapping` — `{ "VarEnfant": "VarParent" }` copie `VarEnfant` de l'enfant vers `VarParent` dans le parent après la complétion.

Si le sous-processus atteint lui-même un nœud d'attente, le parent est aussi mis en pause (`RequiresStop = true`). Au prochain appel à `ContinueAsync` sur le parent, le moteur détecte l'instance enfant existante et la reprend.

---

### 3.8 EndNode

Un `EndNode` termine explicitement une branche. Le moteur marque l'instance comme `Completed` et s'arrête.

```csharp
ProcessBuilder.Create("ProcessusCommande")
    .Business("TraiterApprouve", "Traiter la commande approuvée")
        .Then("Livrer")
    .Business("TraiterRejete", "Traiter la commande rejetée")
        .End("CommandeRejetee", "Commande rejetée")   // la branche se termine ici
    .Business("Livrer", "Livrer la commande")
    .Build();
```

Après `.End(...)`, la chaîne automatique est rompue — le nœud suivant ajouté par le builder commence un segment indépendant. C'est ainsi que l'on définit plusieurs branches terminales dans un seul appel au builder.

Il n'est *pas* nécessaire d'ajouter un `EndNode` pour la fin naturelle d'un processus linéaire. Lorsque `FlowEngine` atteint un nœud sans `NextNodeIds` et que celui-ci se termine sans `RequiresStop`, il passe automatiquement le statut à `Completed`.

---

### 3.9 Paramètres de nœud

Chaque nœud peut porter un `Dictionary<string, object>` statique de paramètres. Ceux-ci sont transmis tels quels à `IBpmMediateur` (et donc à `IBpmCommandHandler` / `IBpmQueryHandler`) lors de l'exécution.

```csharp
ProcessBuilder.Create("ProcessusNotification")
    .Business("EnvoyerEmail", "Envoyer l'e-mail de confirmation")
        .WithParameter("Template", "ConfirmationCommande")
        .WithParameter("Priorite", "Haute")
    .Business("Archiver", "Archiver le document")
        .WithParameters(new()
        {
            ["DureeConservation"] = 90,
            ["Compresser"]        = true
        })
    .Build();
```

`WithParameter` et `WithParameters` opèrent sur le *dernier nœud ajouté*. Les appeler avant qu'un nœud ait été ajouté lève une `InvalidOperationException`.

Réception des paramètres dans un handler :

```csharp
public class EnvoyerEmailHandler : IBpmCommandHandler
{
    public string CommandName => "EnvoyerEmail";

    public Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var template = parameters?["Template"]?.ToString() ?? "Defaut";
        var priorite = parameters?["Priorite"]?.ToString() ?? "Normale";
        // ... logique d'envoi d'e-mail
        return Task.CompletedTask;
    }
}
```

---

### 3.10 Commande OnEnter

Les nœuds bloquants (`Interactive`, `WaitForSignal`, `WaitUntilDate`) peuvent exécuter une commande *juste avant* de se mettre en pause. Utile pour envoyer des notifications ou journaliser.

```csharp
ProcessBuilder.Create("ProcessusApprobation")
    .Business("Preparer", "Préparer les documents")
    .Interactive("ApprobationManager", "Approbation du manager")
        .WithOnEnterCommand("NotifierManager")
        .WithOnEnterCommandParameter("Canal", "email")
    .Business("Archiver", "Archiver")
    .Build();
```

Quand le moteur atteint `ApprobationManager` :
1. `NotifierManager` est dispatché via `IBpmMediateur.ExecuteCommandAsync` avec `{ "Canal": "email" }`.
2. Le moteur se met ensuite en pause avec `WaitingInteraction`.

Si la commande `OnEnter` lève une exception, le nœud échoue (statut `Failed`).

---

### 3.11 Chaînage manuel avec Then et Break

Par défaut, le builder relie chaque nouveau nœud au précédent. Utilisez `.Then()` et `.Break()` pour un contrôle fin.

```csharp
ProcessBuilder.Create("ProcessusFlexi")
    .Business("NoeudA", "A")
        .Then("NoeudC")          // lien explicite : A → C (le chaînage auto vers B est supprimé)
    .Business("NoeudB", "B")     // NoeudB est ajouté mais NON lié automatiquement depuis A
        .Then("NoeudD")
    .Business("NoeudC", "C")     // référencé par NoeudA
    .Business("NoeudD", "D")     // référencé par NoeudB
    .Build();
```

`.Break()` réinitialise le pointeur de nœud courant à `null`, de sorte que le nœud suivant ajouté n'a pas de prédécesseur automatique :

```csharp
ProcessBuilder.Create("BranchesParalleles")
    .Business("Racine", "Nœud racine")
        .Then("BrancheA")
        .Then("BrancheB")
    .Break()
    .Business("BrancheA", "Branche A")
    .Break()
    .Business("BrancheB", "Branche B")
    .Build();
```

---

## 4. Définir un processus — JSON

Les définitions JSON sont utiles pour la configuration externe, le chargement dynamique ou l'intégration d'outils.

```json
{
  "name": "ProcessusCommande",
  "version": "1.0",
  "startNode": "ValiderCommande",
  "nodes": [
    {
      "name": "ValiderCommande",
      "type": "Business",
      "command": "ValiderCommande",
      "parameters": { "ModeStrict": true },
      "next": ["VerifierStock"]
    },
    {
      "name": "VerifierStock",
      "type": "Business",
      "command": "VerifierStock",
      "next": ["DecisionRoutage"]
    },
    {
      "name": "DecisionRoutage",
      "type": "Decision",
      "query": "DecisionRoutage",
      "routes": {
        "en_stock":       "TraiterCommande",
        "rupture_stock":  "CommandeAnterieure"
      }
    },
    {
      "name": "TraiterCommande",
      "type": "Business",
      "command": "TraiterCommande",
      "next": ["AttenteConfirmationPaiement"]
    },
    {
      "name": "AttenteConfirmationPaiement",
      "type": "WaitForSignal",
      "signal": "PaiementRecu"
    },
    {
      "name": "CommandeAnterieure",
      "type": "Interactive",
      "next": ["TraiterCommande"]
    },
    {
      "name": "CommandeRejetee",
      "type": "End"
    }
  ]
}
```

Chargement :

```csharp
using SimpleBPM.Definition;

// Depuis une chaîne JSON
ProcessDefinition processus = ProcessJsonLoader.FromJson(json);

// Depuis un fichier
ProcessDefinition processus = ProcessJsonLoader.FromJsonFile("processus/commande.json");

// Export en JSON
string json = ProcessJsonLoader.ToJson(processus);
```

---

## 5. Implémenter la logique métier

### 5.1 IBpmCommandHandler

Implémentez un `IBpmCommandHandler` par commande métier. La propriété `CommandName` doit correspondre au nom du nœud (ou à la commande passée à `.Business(...)`) dans la définition du processus.

```csharp
using SimpleBPM.Abstractions;

public class ValiderCommandeHandler : IBpmCommandHandler
{
    private readonly ICommandeRepository _commandes;

    public ValiderCommandeHandler(ICommandeRepository commandes)
    {
        _commandes = commandes;
    }

    public string CommandName => "ValiderCommande";

    public async Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var modeStrict = parameters?["ModeStrict"] is true;
        var commande = await _commandes.ObtenirParProcessIdAsync(processId);
        commande.Valider(modeStrict);
        await _commandes.SauvegarderAsync(commande);
    }
}
```

Règles importantes :
- Les constructeurs sont résolus par le conteneur DI — injectez ce dont vous avez besoin.
- Lever une exception marque le nœud (et le processus) comme `Failed`.
- `processId` identifie le workflow en cours ; `aggregateId` est l'identifiant optionnel de l'agrégat métier passé à la création.

### 5.2 IBpmQueryHandler

`IBpmQueryHandler` est utilisé pour les requêtes externes des `DecisionNode`. Le handler doit retourner une chaîne qui correspond à l'une des routes définies sur le nœud de décision.

```csharp
public class DecisionCreditHandler : IBpmQueryHandler
{
    private readonly IServiceCredit _credit;

    public DecisionCreditHandler(IServiceCredit credit)
    {
        _credit = credit;
    }

    public string QueryName => "DecisionCredit";

    public async Task<string> HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var score = await _credit.ObtenirScoreAsync(aggregateId);
        return score >= 650 ? "approuve" : "rejete";
    }
}
```

Si la chaîne retournée ne correspond à aucune route définie, le moteur marque le processus `Failed` avec un message explicite.

### 5.3 IBpmMediateur (approche directe)

Si vous préférez une classe médiateur centralisée plutôt que des handlers individuels, implémentez `IBpmMediateur` directement. Cette approche est mutuellement exclusive avec `AddCommandHandlers` — choisissez l'une ou l'autre.

```csharp
public class MonBpmMediateur : IBpmMediateur
{
    public async Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        switch (commandName)
        {
            case "ValiderCommande":  await ValiderCommandeAsync(processId, parameters); break;
            case "VerifierStock":    await VerifierStockAsync(processId); break;
            default:
                throw new InvalidOperationException($"Commande inconnue : {commandName}");
        }
    }

    public async Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        return decisionName switch
        {
            "DecisionRoutage" => await EvaluerRoutageAsync(processId),
            _ => throw new InvalidOperationException($"Décision inconnue : {decisionName}")
        };
    }

    // ... méthodes privées
}
```

Enregistrement :

```csharp
services.AddSingleton<IBpmMediateur, MonBpmMediateur>();
```

### 5.4 IGestionTache (gestion de tâches optionnelle)

Si votre application dispose d'une boîte de réception de tâches ou d'une file de travail, implémentez `IGestionTache` pour créer et fermer automatiquement des tâches lorsque les `InteractiveNode` sont entrés et quittés.

```csharp
public class MaGestionTache : IGestionTache
{
    private readonly IDepotTaches _taches;

    public MaGestionTache(IDepotTaches taches)
    {
        _taches = taches;
    }

    public async Task CreerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
    {
        await _taches.CreerAsync(new TacheMetier
        {
            ProcessId      = processId,
            AggregateId    = aggregateId,
            NomProcessus   = definitionName,
            NomTache       = nodeName,
            AssigneeA      = DateTime.UtcNow
        });
    }

    public async Task FermerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
    {
        await _taches.FermerAsync(processId, nodeName);
    }
}
```

Le cycle de vie est automatique :
1. Le moteur entre dans un `InteractiveNode` → `CreerTacheAsync` est appelé.
2. Le client appelle `TerminerEtapeEnCoursAsync` → `FermerTacheAsync` est appelé avant la reprise de l'exécution.

---

## 6. Configuration de l'injection de dépendances

### 6.1 Microsoft DI — builder unifié (recommandé)

```csharp
using System.Reflection;
using SimpleBPM.Localisation;

// Program.cs
services.AddSimpleBPM(options =>
{
    // Découverte automatique de tous les IBpmCommandHandler et IBpmQueryHandler de l'assembly
    options.ScanHandlers(Assembly.GetExecutingAssembly());

    // Optionnel : enregistrer un gestionnaire de tâches
    options.UseTaskManager<MaGestionTache>();

    // Persistance Oracle (omettre pour le stockage en mémoire)
    options.UseOracle("CMD");   // préfixe : 3 à 10 lettres majuscules

    // Enregistrer les définitions de processus
    options.AddProcess(ProcessusCommandeDefinitions.CreerProcessusCommande());
    options.AddProcess(ProcessusCommandeDefinitions.CreerProcessusRemboursement());
});
```

Puis enregistrez la connexion Oracle :

```csharp
services.AddScoped<IDbConnection>(sp =>
{
    var conn = new OracleConnection(configuration.GetConnectionString("Oracle"));
    conn.Open();
    return conn;
});
```

### 6.2 Microsoft DI — étape par étape

Pour un contrôle plus fin :

```csharp
// 1. Enregistrer les handlers de commandes / requêtes (découverte automatique)
services.AddCommandHandlers(Assembly.GetExecutingAssembly());

// 2. Services optionnels
services.AddSingleton<IGestionTache, MaGestionTache>();

// 3. Connexion Oracle (gérée par le client)
services.AddScoped<IDbConnection>(sp =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return conn;
});

// 4. Définitions de processus (chacune en singleton)
services.AddSingleton(ProcessusCommandeDefinitions.CreerProcessusCommande());
services.AddSingleton(ProcessusCommandeDefinitions.CreerProcessusRemboursement());

// 5. SimpleBPM principal (backend Oracle)
services.AddSimpleBPM(tablePrefix: "CMD");
// ou en mémoire :
// services.AddSimpleBPM();
```

### 6.3 Module Autofac

```csharp
using Autofac;
using SimpleBPM.Localisation;

var builder = new ContainerBuilder();

builder.RegisterModule(new RegistrationBpmModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<MaGestionTache>();
    module.UseOracle("CMD");
    module.AddProcess(ProcessusCommandeDefinitions.CreerProcessusCommande());
}));

// Connexion Oracle — enregistrée séparément
builder.Register(ctx =>
{
    var conn = new OracleConnection(connectionString);
    conn.Open();
    return (IDbConnection)conn;
}).InstancePerLifetimeScope();

var container = builder.Build();
```

### 6.4 Persistance Oracle

`OracleProcessRepository` nécessite une `IDbConnection` ouverte (injectée par scope). Il utilise Dapper pour toutes les requêtes.

**Règles du préfixe de table** :
- Entre 3 et 10 caractères.
- Lettres uniquement (pas de chiffres ni de caractères spéciaux).
- Converti automatiquement en majuscules.

**Tables créées** :

| Table | Description |
|---|---|
| `{PREFIXE}_PROCESS_CONTEXT` | Une ligne par instance de processus — état, variables (JSON), horodatages |
| `{PREFIXE}_HISTORIQUE_EXECUTION_NOEUD` | Une ligne par exécution de nœud — journal d'audit avec durées |

Initialisez le schéma une fois par environnement (ex. au démarrage de l'application) :

```csharp
var repo = serviceProvider.GetRequiredService<IProcessRepository>();
if (repo is OracleProcessRepository oracleRepo)
    await oracleRepo.InitializeDatabaseAsync();
```

Ou appliquez `schema.sql` manuellement pour les environnements où l'application ne doit pas créer les tables automatiquement.

### 6.5 Banque de définitions

La **banque de définitions** permet de sauvegarder des `ProcessDefinition` en base de données (Oracle ou en mémoire) afin de centraliser et versionner les définitions de processus indépendamment du code applicatif.

#### Activation

**Microsoft DI :**

```csharp
services.AddSimpleBPM(options =>
{
    options.ScanHandlers(Assembly.GetExecutingAssembly());
    options.UseOracle("CMD");
    options.UseDefinitionBank();   // active IDefinitionRepository (Oracle si UseOracle, sinon mémoire)
    options.AddProcess(MonProcessus.Creer());
});
```

**Autofac :**

```csharp
builder.RegisterModule(new RegistrationBpmModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseOracle("CMD");
    module.UseDefinitionBank();
    module.AddProcess(MonProcessus.Creer());
}));
```

#### Sauvegarder et charger des définitions

```csharp
var flowService = scope.Resolve<IFlowService>();

// Sauvegarder une définition dans la banque
var definition = ProcessBuilder.Create("MonProcessus", "2.0")
    .Business("Etape1", "Première étape")
    .Build();

await flowService.SauvegarderDefinitionAsync(definition);

// Charger toutes les définitions disponibles dans la banque
List<ProcessDefinition> definitions = await flowService.ObtenirDefinitionsAsync();
```

#### Table Oracle

Lorsque `UseOracle` est combiné avec `UseDefinitionBank`, une table supplémentaire est créée :

| Table | Description |
|---|---|
| `{PREFIXE}_DEFINITION_BANQUE` | Une ligne par version de définition — contenu JSON sérialisé |

```sql
-- Initialisez via InitializeDatabaseAsync() ou manuellement via schema.sql
```

#### Résolution automatique depuis la banque

Lorsque la banque est activée, le moteur cherche les définitions dans l'ordre suivant :
1. **En mémoire** (définitions enregistrées via `AddProcess` dans le DI).
2. **Dans la banque** (`IDefinitionRepository`) si la définition n'est pas trouvée en mémoire.

Cela permet de déployer de nouvelles versions de processus sans redémarrer l'application.

---

## 7. Exploiter les processus à l'exécution

Toutes les interactions à l'exécution passent par `IFlowService`.

```csharp
public interface IFlowService
{
    Task<long>                CreateProcessInstanceAsync(string definitionName, Dictionary<string, object>? variables = null);
    Task<Processus>           ObtenirAsync(long instanceProcessId);
    Task                      TerminerEtapeAsync(long idInstanceNoeud, object contenu);
    Task                      TerminerEtapeEnCoursAsync(long idInstanceProcessus, Dictionary<string, object>? contenu = null);
    Task                      EnvoyerSignalAsync(long idInstanceProcessus, string signalName);
    Task<IEnumerable<string>> ObtenirSignauxEnAttenteAsync(long idInstanceProcessus);
    Task<NoeudProcessus>        ObtenirNoeudAsync(long idInstanceNoeud);
    Task<List<Processus>>     RechercherParVariableAsync(List<FiltreVariable> filtres);
    Task<List<Processus>>     ObtenirEnfantsAsync(long idInstanceParent);
    Task<MigrationResult>     MigrateAsync(long processId, ProcessDefinition targetDefinition, ProcessMigration migration);

    // Banque de définitions (requiert UseDefinitionBank())
    Task                      SauvegarderDefinitionAsync(ProcessDefinition definition);
    Task<List<ProcessDefinition>> ObtenirDefinitionsAsync();
}
```

### 7.1 Créer une instance de processus

```csharp
// IFlowService injecté via DI
private readonly IFlowService _flowService;

long processId = await _flowService.CreateProcessInstanceAsync("ProcessusCommande", new()
{
    ["CommandeId"]  = "CMD-001",
    ["Montant"]     = 2500.00,
    ["ClientId"]    = "CLI-42"
});
```

`CreateProcessInstanceAsync` démarre l'exécution immédiatement et enchaîne les nœuds jusqu'à rencontrer un nœud d'attente ou la fin du processus.
Le `long` retourné est l'identifiant unique de l'instance (issu d'une séquence Oracle ou du compteur en mémoire).

### 7.2 Terminer une étape interactive

Deux méthodes sont disponibles :

**Par identifiant de processus** (le plus courant) :

```csharp
await _flowService.TerminerEtapeEnCoursAsync(processId, new()
{
    ["DecisionManager"] = "approuve",
    ["Commentaire"]     = "Tous les contrôles sont passés."
});
```

**Par identifiant d'instance de nœud** (quand vous avez stocké l'enregistrement du nœud) :

```csharp
await _flowService.TerminerEtapeAsync(idInstanceNoeud, new Dictionary<string, object>
{
    ["DecisionManager"] = "approuve"
});
```

Les deux méthodes :
1. Fusionnent le dictionnaire fourni dans `instance.Variables`.
2. Appellent `OnLeaveAsync` sur le handler du nœud courant (ferme la tâche si `IGestionTache` est enregistré).
3. Reprennent l'exécution à partir du nœud suivant.

### 7.3 Envoyer un signal

```csharp
await _flowService.EnvoyerSignalAsync(processId, "PaiementRecu");
```

Si le processus est en `WaitingSignal` et que le nom du signal correspond au signal attendu, l'exécution reprend immédiatement.

Pour découvrir quels signaux un processus attend :

```csharp
IEnumerable<string> signaux = await _flowService.ObtenirSignauxEnAttenteAsync(processId);
// ex. ["PaiementRecu"]
```

### 7.4 Consulter l'état d'un processus

```csharp
Processus processus = await _flowService.ObtenirAsync(processId);

Console.WriteLine(processus.Status);         // Running, WaitingInteraction, Completed, ...
Console.WriteLine(processus.CurrentNodeId);  // Nom du nœud courant
Console.WriteLine(processus.AggregateId);    // Lien vers l'agrégat métier
```

`Processus` est un DTO en lecture seule — il n'expose pas l'état interne du moteur.

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

### 7.5 Rechercher par variable

Utilisez `RechercherParVariableAsync` pour trouver des instances de processus selon leurs valeurs de variables. Tous les filtres sont combinés avec une logique ET.

```csharp
using SimpleBPM;

// Condition unique (égalité)
var resultats = await _flowService.RechercherParVariableAsync(new()
{
    new FiltreVariable("ClientId", "CLI-42", OperateurFiltre.Egal, TypeDonnee.Texte)
});

// Plusieurs conditions
var resultats = await _flowService.RechercherParVariableAsync(new()
{
    new FiltreVariable("Montant",     1_000, OperateurFiltre.SuperieurOuEgal, TypeDonnee.Nombre),
    new FiltreVariable("Statut",    "Ouvert", OperateurFiltre.Egal,           TypeDonnee.Texte),
    new FiltreVariable("CreeLe",    DateTime.Today.AddDays(-7), OperateurFiltre.Superieur, TypeDonnee.Date)
});
```

Les mêmes opérateurs et types de données que pour `DecisionNode` sont disponibles.

---

## 8. Surveillance

Pour les tableaux de bord, panneaux d'administration ou services de reporting nécessitant un accès en lecture seule à l'état des processus, utilisez `IProcessMonitor` plutôt que `IFlowService`.

```csharp
// Enregistrement (Microsoft DI)
services.AddProcessMonitoring();   // aucun moteur, aucun IBpmMediateur requis

// ou dans un module Autofac de tableau de bord :
builder.RegisterModule(new ProcessMonitoringAutofacModule());
```

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

Exemple — tableau de bord affichant tous les processus en attente :

```csharp
var enAttente = await _monitor.GetInstancesByStatusAsync(ProcessStatus.WaitingInteraction);
foreach (var p in enAttente)
{
    Console.WriteLine($"{p.DefinitionName} #{p.Id} — en attente sur : {p.CurrentNodeId}");
}

// Comptage par statut pour un widget de synthèse
var synthese = await _monitor.GetStatusSummaryAsync();
// { Running: 5, WaitingInteraction: 12, Completed: 348, Failed: 2 }
```

---

## 9. Migration de version

Lors de la publication d'une nouvelle version d'une définition de processus, les instances actuellement en pause (en attente) peuvent être migrées sans perte de données.

Seules les instances dans les états `WaitingInteraction`, `WaitingSignal` ou `WaitingDate` peuvent être migrées — les instances en cours d'exécution ne peuvent pas être interrompues.

### Définir une migration

```csharp
var migration = new ProcessMigration("1.0", "2.0")
    .MapNode("Revue", "RevueDetaillee")            // nœud renommé
    .SetVariable("MigreDepuisV1", true)            // ajouter une nouvelle variable
    .RenameVariable("AncienStatut", "StatutRevue") // renommer une variable
    .RemoveVariable("DrapeauObsolete");             // supprimer une variable obsolète
```

Ou chargez depuis JSON :

```json
{
  "fromVersion": "1.0",
  "toVersion":   "2.0",
  "nodeMappings": {
    "Revue": "RevueDetaillee"
  },
  "variableTransforms": [
    { "type": "set",    "name": "MigreDepuisV1",  "value": true },
    { "type": "rename", "name": "AncienStatut",    "newName": "StatutRevue" },
    { "type": "remove", "name": "DrapeauObsolete" }
  ]
}
```

```csharp
var migration = ProcessMigrationLoader.FromJsonFile("migrations/v1_vers_v2.json");
```

### Appliquer la migration

```csharp
var definitionV2 = ProcessusCommandeDefinitions.CreerProcessusCommandeV2();

MigrationResult resultat = await _flowService.MigrateAsync(processId, definitionV2, migration);

if (resultat.Success)
    Console.WriteLine($"Migré de {resultat.PreviousVersion} vers {resultat.NewVersion}");
else
    Console.WriteLine($"Échec de la migration : {resultat.ErrorMessage}");
```

**Types de transformations de variables** :

| Type | Effet |
|---|---|
| `set` | Crée ou écrase une variable avec la valeur donnée |
| `rename` | Renomme une clé de variable (valeur conservée) |
| `remove` | Supprime une variable |

---

## 10. Exemple complet — workflow d'approbation de prêt

Cette section présente une intégration réaliste de bout en bout, basée sur le projet `SimpleBPM.ExampleClient`.

### Définition du processus

```csharp
using SimpleBPM;
using SimpleBPM.Definition;

public static class ProcessusPretDefinitions
{
    public static ProcessDefinition CreerProcessusApprobationPret()
    {
        var sousProcessusVerification = ProcessBuilder.Create("ProcessusVerification", "1.0")
            .Business("VerifierIdentite",   "Vérifier les pièces d'identité")
            .Business("VerifierRevenus",    "Vérifier les justificatifs de revenus")
            .Business("VerifierEmploi",     "Vérifier la situation professionnelle")
            .Build();

        return ProcessBuilder.Create("ApprobationPret", "1.0")
            // Valider la demande et attacher la liste des pièces requises
            .Business("ValiderDemande", "Valider la demande de prêt")
                .WithParameter("PiecesRequises", "CI,Revenus,Emploi")

            // Exécuter un sous-processus de vérification du demandeur avec mapping de variables
            .SubProcess("Verification", sousProcessusVerification,
                inputMapping:  new() { ["IdDemandeur"] = "IdDemandeur" },
                outputMapping: new() { ["VerificationReussie"] = "EstVerifie" },
                displayName: "Vérification du demandeur")

            // Vérifier le score de crédit
            .Business("VerifierCredit", "Vérifier le score de crédit")

            // Router selon la décision de crédit (retournée par DecisionCreditHandler)
            .Decision("DecisionCredit", "Décision de crédit", routes => routes
                .When("approuve", "CalculerConditions")
                .When("rejete",   "RejeterDemande"))

            // Branche approuvée
            .Business("CalculerConditions", "Calculer les conditions du prêt")
                .Then("RevueManuelle")        // éviter le chaînage auto vers la branche rejetée
            .Business("RejeterDemande", "Rejeter la demande de prêt")
                .End("PretRejete", "Demande de prêt rejetée")   // la branche se termine ici

            // Étape interactive — pause et attente de l'analyste
            .Interactive("RevueManuelle", "Revue analyste")

            // Attendre la signature des documents par le demandeur
            .WaitForSignal("AttenteSignatureDocuments", "Attente de la signature des documents")

            // Décaisser et clôturer
            .Business("DecaisserFonds", "Décaisser les fonds")
                .End("PretApprouve", "Prêt approuvé et décaissé")

            .Build();
    }
}
```

### Handlers de commandes

```csharp
// ValiderDemandeHandler.cs
public class ValiderDemandeHandler : IBpmCommandHandler
{
    public string CommandName => "ValiderDemande";

    public Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        var pieces = parameters?["PiecesRequises"]?.ToString() ?? "";
        Console.WriteLine($"[ValiderDemande] Pièces requises : {pieces}");
        return Task.CompletedTask;
    }
}

// DecaisserFondsHandler.cs
public class DecaisserFondsHandler : IBpmCommandHandler
{
    private readonly IPasserelleDecaissement _passerelle;

    public DecaisserFondsHandler(IPasserelleDecaissement passerelle) => _passerelle = passerelle;

    public string CommandName => "DecaisserFonds";

    public async Task HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        await _passerelle.DecaisserAsync(processId);
    }
}
```

### Handler de décision

```csharp
// DecisionCreditHandler.cs
public class DecisionCreditHandler : IBpmQueryHandler
{
    private readonly IBureauCredit _bureau;

    public DecisionCreditHandler(IBureauCredit bureau) => _bureau = bureau;

    public string QueryName => "DecisionCredit";

    public async Task<string> HandleAsync(long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        int score = await _bureau.ObtenirScoreAsync(aggregateId);
        return score >= 650 ? "approuve" : "rejete";
    }
}
```

### Gestionnaire de tâches

```csharp
public class GestionTachePret : IGestionTache
{
    private readonly IDepotTaches _depot;

    public GestionTachePret(IDepotTaches depot) => _depot = depot;

    public Task CreerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
        => _depot.CreerAsync(processId, nodeName, "Analyste");

    public Task FermerTacheAsync(long processId, long? aggregateId,
        string definitionName, string nodeName)
        => _depot.FermerAsync(processId, nodeName);
}
```

### Configuration DI (Autofac)

```csharp
var containerBuilder = new ContainerBuilder();

containerBuilder.RegisterModule(new RegistrationBpmModule(module =>
{
    module.ScanHandlers(Assembly.GetExecutingAssembly());
    module.UseTaskManager<GestionTachePret>();
    module.AddProcess(ProcessusPretDefinitions.CreerProcessusApprobationPret());
    // module.UseOracle("PT");   // décommenter pour Oracle
}));

containerBuilder.RegisterType<BureauCreditFactice>().As<IBureauCredit>().SingleInstance();
containerBuilder.RegisterType<PasserelleDecaissementFactice>().As<IPasserelleDecaissement>().SingleInstance();

var container = containerBuilder.Build();
```

### Utilisation à l'exécution

```csharp
await using var scope = container.BeginLifetimeScope();
var flowService = scope.Resolve<IFlowService>();

// 1. Démarrer le workflow
long processId = await flowService.CreateProcessInstanceAsync("ApprobationPret", new()
{
    ["IdDemandeur"] = "DEM-12345",
    ["MontantPret"] = 50_000.00,
    ["DureePret"]   = 36
});

var processus = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Statut : {processus.Status}");   // WaitingInteraction

// 2. L'analyste complète la revue manuelle
await flowService.TerminerEtapeEnCoursAsync(processId, new()
{
    ["DecisionAnalyste"] = "approuve",
    ["Notes"]            = "Tous les documents vérifiés."
});

processus = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Statut : {processus.Status}");   // WaitingSignal

// 3. Le demandeur signe les documents — le système externe envoie le signal
var signaux = await flowService.ObtenirSignauxEnAttenteAsync(processId);
// signaux == ["AttenteSignatureDocuments"]

await flowService.EnvoyerSignalAsync(processId, "AttenteSignatureDocuments");

processus = await flowService.ObtenirAsync(processId);
Console.WriteLine($"Statut : {processus.Status}");   // Completed
```

---

## Référence rapide

| Action | Méthode |
|---|---|
| Démarrer un processus | `IFlowService.CreateProcessInstanceAsync` |
| Terminer une étape interactive | `IFlowService.TerminerEtapeEnCoursAsync` |
| Terminer une étape par ID de nœud | `IFlowService.TerminerEtapeAsync` |
| Envoyer un signal | `IFlowService.EnvoyerSignalAsync` |
| Consulter l'état d'un processus | `IFlowService.ObtenirAsync` |
| Obtenir les signaux en attente | `IFlowService.ObtenirSignauxEnAttenteAsync` |
| Rechercher par variable | `IFlowService.RechercherParVariableAsync` |
| Obtenir les processus enfants | `IFlowService.ObtenirEnfantsAsync` |
| Migrer vers une nouvelle version | `IFlowService.MigrateAsync` |
| Sauvegarder une définition dans la banque | `IFlowService.SauvegarderDefinitionAsync` |
| Charger toutes les définitions de la banque | `IFlowService.ObtenirDefinitionsAsync` |
| Surveiller (lecture seule) | `IProcessMonitor.*` |
