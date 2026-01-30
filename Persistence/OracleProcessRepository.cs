using System.Data;
using Dapper;

namespace SimpleBPM.Persistence;

public class OracleProcessRepository : IProcessRepository
{
    private readonly OracleConfiguration _config;
    private readonly IDbConnection _connection;
    private readonly string _processContextTable;
    private readonly OracleHistoryRepository _historyRepository;

    public OracleProcessRepository(OracleConfiguration config, IDbConnection connection)
    {
        _config = config;
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _processContextTable = _config.GetTableName("PROCESS_CONTEXT");
        _historyRepository = new OracleHistoryRepository(config, connection);
    }

    public async Task InitializeDatabaseAsync()
    {
        var createTableSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE TABLE {_processContextTable} (
                    ID_PROCESSUS VARCHAR2(255) PRIMARY KEY,
                    ID_AGREGAT VARCHAR2(255),
                    DONNEES CLOB,
                    DATE_DEBUT TIMESTAMP,
                    DATE_DERNIERE_EXECUTION TIMESTAMP,
                    DATE_COMPLETION TIMESTAMP,
                    ID_NOEUD_COURANT VARCHAR2(255),
                    NOM_DEFINITION VARCHAR2(255),
                    VERSION_DEFINITION VARCHAR2(50),
                    SOUS_PROCESSUS CLOB,
                    STATUT NUMBER(10),
                    CONSTRAINT CHK_{_config.TablePrefix}_STATUT CHECK (STATUT BETWEEN 0 AND 5)
                )';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        await _connection.ExecuteAsync(createTableSql);

        var createIndex01Sql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_01_PROCESS_CONTEXT ON {_processContextTable}(ID_AGREGAT)';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        var createIndex02Sql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_02_PROCESS_CONTEXT ON {_processContextTable}(STATUT)';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        var createIndex03Sql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_03_PROCESS_CONTEXT ON {_processContextTable}(DATE_DEBUT)';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        await _connection.ExecuteAsync(createIndex01Sql);
        await _connection.ExecuteAsync(createIndex02Sql);
        await _connection.ExecuteAsync(createIndex03Sql);

        await _historyRepository.InitializeDatabaseAsync();
    }

    public async Task SaveProcessInstanceAsync(ProcessInstance instance)
    {
        var sql = $@"
            INSERT INTO {_processContextTable}
            (ID_PROCESSUS, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, SOUS_PROCESSUS, STATUT)
            VALUES
            (:IdProcessus, :IdAgregat, :Donnees, :DateDebut, :DateDerniereExecution, :DateCompletion, :IdNoeudCourant, :NomDefinition, :VersionDefinition, :SousProcessus, :Statut)";

        var parameters = new
        {
            IdProcessus = instance.ProcessId,
            IdAgregat = instance.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(instance.Variables),
            DateDebut = instance.StartedAt,
            DateDerniereExecution = instance.LastExecutedAt,
            DateCompletion = instance.CompletedAt,
            IdNoeudCourant = instance.CurrentNodeId,
            NomDefinition = instance.DefinitionName,
            VersionDefinition = instance.DefinitionVersion,
            SousProcessus = instance.SubProcessIds.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(instance.SubProcessIds)
                : null,
            Statut = (int)instance.Status
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task<ProcessInstance?> GetProcessInstanceAsync(string processId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, SOUS_PROCESSUS, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS = :IdProcessus";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessInstanceDto>(sql, new { IdProcessus = processId });

        if (result == null)
        {
            return null;
        }

        var instance = MapToInstance(result);
        instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(processId);

        return instance;
    }

    public async Task UpdateProcessInstanceAsync(ProcessInstance instance)
    {
        var sql = $@"
            UPDATE {_processContextTable}
            SET ID_AGREGAT = :IdAgregat,
                DONNEES = :Donnees,
                DATE_DERNIERE_EXECUTION = :DateDerniereExecution,
                DATE_COMPLETION = :DateCompletion,
                ID_NOEUD_COURANT = :IdNoeudCourant,
                NOM_DEFINITION = :NomDefinition,
                VERSION_DEFINITION = :VersionDefinition,
                SOUS_PROCESSUS = :SousProcessus,
                STATUT = :Statut
            WHERE ID_PROCESSUS = :IdProcessus";

        var parameters = new
        {
            IdAgregat = instance.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(instance.Variables),
            DateDerniereExecution = instance.LastExecutedAt,
            DateCompletion = instance.CompletedAt,
            IdNoeudCourant = instance.CurrentNodeId,
            NomDefinition = instance.DefinitionName,
            VersionDefinition = instance.DefinitionVersion,
            SousProcessus = instance.SubProcessIds.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(instance.SubProcessIds)
                : null,
            Statut = (int)instance.Status,
            IdProcessus = instance.ProcessId
        };

        await _connection.ExecuteAsync(sql, parameters);

        var existingHistory = await _historyRepository.GetHistoryAsync(instance.ProcessId);
        var newHistories = instance.ExecutionHistory.Skip(existingHistory.Count).ToList();

        if (newHistories.Count > 0)
        {
            await _historyRepository.SaveHistoryBatchAsync(instance.ProcessId, newHistories);
        }
    }

    public async Task DeleteProcessInstanceAsync(string processId)
    {
        var sql = $@"DELETE FROM {_processContextTable} WHERE ID_PROCESSUS = :IdProcessus";
        await _connection.ExecuteAsync(sql, new { IdProcessus = processId });
    }

    public async Task<List<ProcessInstance>> SearchByVariableAsync(Dictionary<string, object> variablesFiltre)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, SOUS_PROCESSUS, STATUT
            FROM {_processContextTable}
            WHERE DONNEES IS NOT NULL";

        var results = await _connection.QueryAsync<ProcessInstanceDto>(sql);
        var matches = new List<ProcessInstance>();

        foreach (var result in results)
        {
            var variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>();

            var allMatch = variablesFiltre.All(filter =>
                variables.TryGetValue(filter.Key, out var value) &&
                value?.ToString() == filter.Value?.ToString());

            if (allMatch)
            {
                var instance = MapToInstance(result);
                instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.ID_PROCESSUS);
                matches.Add(instance);
            }
        }

        return matches;
    }

    public async Task<(NodeExecutionHistory History, string ProcessId)?> GetNodeHistoryByIdAsync(string historyId)
    {
        return await _historyRepository.GetByIdAsync(historyId);
    }

    private ProcessInstance MapToInstance(ProcessInstanceDto result)
    {
        return new ProcessInstance(result.ID_PROCESSUS)
        {
            AggregateId = result.ID_AGREGAT,
            DefinitionName = result.NOM_DEFINITION,
            DefinitionVersion = result.VERSION_DEFINITION,
            Variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>(),
            SubProcessIds = string.IsNullOrEmpty(result.SOUS_PROCESSUS)
                ? new Dictionary<string, string>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(result.SOUS_PROCESSUS) ?? new Dictionary<string, string>(),
            StartedAt = result.DATE_DEBUT,
            LastExecutedAt = result.DATE_DERNIERE_EXECUTION,
            CompletedAt = result.DATE_COMPLETION,
            CurrentNodeId = result.ID_NOEUD_COURANT,
            Status = (ProcessStatus)result.STATUT
        };
    }

    private class ProcessInstanceDto
    {
        public string ID_PROCESSUS { get; set; } = string.Empty;
        public string? ID_AGREGAT { get; set; }
        public string DONNEES { get; set; } = string.Empty;
        public DateTime DATE_DEBUT { get; set; }
        public DateTime? DATE_DERNIERE_EXECUTION { get; set; }
        public DateTime? DATE_COMPLETION { get; set; }
        public string? ID_NOEUD_COURANT { get; set; }
        public string? NOM_DEFINITION { get; set; }
        public string? VERSION_DEFINITION { get; set; }
        public string? SOUS_PROCESSUS { get; set; }
        public int STATUT { get; set; }
    }
}
