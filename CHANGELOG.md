# Changelog

Historique des modifications du projet SimpleBPM.

## Non publié

### Ajouté
- Support des paramètres sur les nœuds (`ProcessNode.Parameters`) : dictionnaire `Dictionary<string, object>` transmis à `ICommandExecutor` lors de l'exécution
- API fluide `WithParameter(key, value)` et `WithParameters(dict)` sur `ProcessBuilder`
- Support des paramètres dans la sérialisation/désérialisation JSON (`ProcessJsonLoader`)
- Projet de tests unitaires `SimpleBPM.Tests` avec xUnit — couverture complète incluant `ConditionDecisionTests`, `FiltreVariableTests`, `FlowEngineTests`, `HandlersTests`, `NodeExecutionHistoryTests`, `OracleConfigurationTests`, `ProcessBuilderTests`, `ProcessDefinitionTests`, `ProcessInstanceTests`, `ProcessJsonLoaderTests`, `ProcessNodeTests`, `ProcessusTests`
- Conditions à base d'opérateurs pour les nœuds de décision (`ConditionDecision`)
- Filtre de recherche par variable avec opérateurs (`FiltreVariable`, `OperateurFiltre`, `TypeDonnee`)
- Enregistrement DI via `AddSimpleBPM` dans `Localisation/ServiceCollectionExtensions.cs`
- Fichier `.gitignore`
- Ce fichier `CHANGELOG.md`

### Modifié
- `ICommandExecutor.ExecuteCommandAsync` et `EvaluateDecisionAsync` acceptent un paramètre optionnel `Dictionary<string, object>? parameters`
- IDs de processus et nœuds passés de `string` à `long` (NUMBER(10) en Oracle, séquences via `ObtenirSequenceAsync`)
- `ProcessNode.Id` supprimé : `Name` sert d'identifiant, `DisplayName` pour l'affichage
- Suivi des sous-processus via `ParentProcessId` au lieu de `SubProcessIds`
- `RechercherParVariable` utilise `List<FiltreVariable>` au lieu d'un dictionnaire simple
- `AddSimpleBPM` ne prend plus que `tablePrefix` ; la connexion `IDbConnection` est enregistrée par le client
- Constructeur `OracleConfiguration(string tablePrefix)` ajouté (sans connection string)
- Consolidation de `ExampleWithHistory` dans `ExampleWithOracle` (suppression du doublon)
- Mise à jour du `README.md` (paramètres, structure du projet, section DI, section tests)

## 1.0 - Version initiale

### Ajouté
- Moteur BPM (`FlowEngine`) avec exécution multi-définitions et multi-versions
- Service client (`FlowService` / `IFlowService`) compatible BPM existant
- 6 types de noeuds : Business, Decision, Interactive, WaitForSignal, WaitUntilDate, SubProcess
- Fluent Builder (`ProcessBuilder`) avec IntelliSense et validation
- Chargeur/exporteur JSON (`ProcessJsonLoader`)
- Sous-processus avec `InputMapping` / `OutputMapping` explicite
- Persistance Oracle (`OracleProcessRepository`) avec préfixe de tables configurable
- Migration de version pour instances en attente (`ProcessMigration`, `ProcessMigrationRunner`)
- Transformations de variables lors de la migration (set, rename, remove)
- Interface `IGestionTache` optionnelle pour la gestion de tâches sur noeuds interactifs
- Handlers par type de noeud avec injection de dépendances
- Auto-enregistrement des handlers par défaut (Interactive, WaitForSignal, WaitUntilDate, SubProcess)
- Historique d'exécution (`NodeExecutionHistory`) avec suivi de durée
- Exemples d'utilisation (4 fichiers dans `Examples/`)
- Script SQL Oracle (`schema.sql`)
