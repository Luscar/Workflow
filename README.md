# SimpleBPM - Librairie BPM simple en C#

Une librairie légère pour gérer des processus métier (BPM) avec différents types de nœuds et persistance Oracle.

## Types de nœuds

- **BusinessNode** : Exécute une commande ou query métier
- **DecisionNode** : Permet de router vers différents nœuds selon le résultat d'une query
- **InteractiveNode** : Arrête le processus en attente d'interaction utilisateur
- **WaitUntilDateNode** : Arrête le processus jusqu'à une date précise
- **WaitForSignalNode** : Arrête le processus en attente d'un signal spécifique
- **SubProcessNode** : Exécute un sous-processus complet avec gestion d'état

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
    InheritAggregateId = true,
    SaveSubProcessState = true
};
```

Voir `ExampleWithSubProcess.cs` pour un exemple complet.

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
```

**Note** : Le repository ne gère pas le cycle de vie de la connexion. C'est la responsabilité du client (ou du container DI) de l'ouvrir et la fermer.

### Préfixe de tables

- Le préfixe doit contenir entre 3 et 10 lettres
- Seules les lettres sont acceptées (pas de chiffres ou caractères spéciaux)
- Le préfixe est automatiquement converti en majuscules
- Exemple : préfixe "ABC" → table "ABC_PROCESS_CONTEXT"

### Tables créées

- `{PREFIX}_PROCESS_CONTEXT` : Stocke les contextes d'exécution des processus
- `{PREFIX}_HISTORIQUE_EXECUTION_NOEUD` : Historique détaillé de chaque étape

## Utilisation

### Sans persistance

Voir `Example.cs` pour un exemple simple sans base de données.

### Avec persistance Oracle

Voir `ExampleWithOracle.cs` pour un exemple complet avec Oracle.

```csharp
// Créer le moteur avec repository
var engine = new ProcessEngine(processDefinition, repository);

// Exécuter un processus (sauvegardé automatiquement)
var context = await engine.ExecuteAsync(new ProcessContext("order-123"));

// Charger un processus existant
var loadedContext = await engine.LoadProcessAsync("order-123");

// Continuer l'exécution
await engine.ContinueAsync(loadedContext);
```

### Avec historique

Voir `ExampleWithHistory.cs` pour voir comment analyser l'historique d'exécution.

## Architecture

- Le processus s'exécute nœud par nœud jusqu'à rencontrer un nœud d'arrêt ou la fin naturelle
- Les nœuds métier et décisionnels appellent des commandes/queries via leur nom et l'ID du processus/agrégat
- L'application cliente implémente `ICommandQueryExecutor` pour définir comment exécuter les commandes/queries
- Le contexte est automatiquement sauvegardé/mis à jour dans Oracle après chaque exécution
- Les sous-processus peuvent être imbriqués et sont gérés de manière transparente

## Script SQL

Un script SQL manuel est disponible dans `schema.sql` pour créer les tables manuellement si nécessaire.
