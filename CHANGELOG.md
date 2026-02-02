# Changelog

Historique des modifications du projet SimpleBPM.

## Non publié

### Ajouté
- Projet de tests unitaires `SimpleBPM.Tests` avec xUnit (73 tests)
- Enregistrement DI via `AddSimpleBPM` dans `Localisation/ServiceCollectionExtensions.cs`
- Fichier `.gitignore`
- Ce fichier `CHANGELOG.md`

### Modifié
- `AddSimpleBPM` ne prend plus que `tablePrefix` ; la connexion `IDbConnection` est enregistrée par le client
- Constructeur `OracleConfiguration(string tablePrefix)` ajouté (sans connection string)
- Consolidation de `ExampleWithHistory` dans `ExampleWithOracle` (suppression du doublon)
- Mise à jour du `README.md` (structure du projet, section DI, section tests)

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
