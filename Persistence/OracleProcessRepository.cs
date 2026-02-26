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

    public async Task<long> ObtenirSequenceAsync(string nomSequence)
    {
        var sequenceName = _config.GetTableName(nomSequence);
        var sql = $"SELECT {sequenceName}.NEXTVAL FROM DUAL";
        return await _connection.ExecuteScalarAsync<long>(sql);
    }

    public async Task InitializeDatabaseAsync()
    {
        var createSequenceProcessusSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE SEQUENCE {_config.GetTableName("SEQ_PROCESSUS")} START WITH 1 INCREMENT BY 1 NOCACHE';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        var createSequenceHistoriqueSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE SEQUENCE {_config.GetTableName("SEQ_HISTORIQUE")} START WITH 1 INCREMENT BY 1 NOCACHE';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        await _connection.ExecuteAsync(createSequenceProcessusSql);
        await _connection.ExecuteAsync(createSequenceHistoriqueSql);

        var createTableSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE TABLE {_processContextTable} (
                    ID_PROCESSUS NUMBER(10) PRIMARY KEY,
                    ID_PROCESSUS_PARENT NUMBER(10),
                    ID_NOEUD_PARENT VARCHAR2(255),
                    ID_AGREGAT VARCHAR2(255),
                    DONNEES CLOB,
                    DATE_ATTENTE TIMESTAMP,
                    SIGNAL_ATTENTE VARCHAR2(255),
                    DATE_DEBUT TIMESTAMP,
                    DATE_DERNIERE_EXECUTION TIMESTAMP,
                    DATE_COMPLETION TIMESTAMP,
                    ID_NOEUD_COURANT VARCHAR2(255),
                    NOM_DEFINITION VARCHAR2(255),
                    VERSION_DEFINITION VARCHAR2(50),
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

        var addDateAttenteColumnSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE {_processContextTable} ADD (DATE_ATTENTE TIMESTAMP)';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -1430 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        var addSignalAttenteColumnSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE {_processContextTable} ADD (SIGNAL_ATTENTE VARCHAR2(255))';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -1430 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        await _connection.ExecuteAsync(createTableSql);
        await _connection.ExecuteAsync(addDateAttenteColumnSql);
        await _connection.ExecuteAsync(addSignalAttenteColumnSql);

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
            (ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_ATTENTE, SIGNAL_ATTENTE, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT)
            VALUES
            (:IdProcessus, :IdProcessusParent, :IdNoeudParent, :IdAgregat, :Donnees, :DateAttente, :SignalAttente, :DateDebut, :DateDerniereExecution, :DateCompletion, :IdNoeudCourant, :NomDefinition, :VersionDefinition, :Statut)";

        var parameters = new
        {
            IdProcessus = instance.ProcessId,
            IdProcessusParent = instance.ParentProcessId,
            IdNoeudParent = instance.ParentNodeName,
            IdAgregat = instance.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(instance.Variables),
            DateAttente = instance.InternalState.TryGetValue("WaitUntilDate", out var d) ? (DateTime?)d : null,
            SignalAttente = instance.InternalState.TryGetValue("WaitingForSignal", out var s) ? s?.ToString() : null,
            DateDebut = instance.StartedAt,
            DateDerniereExecution = instance.LastExecutedAt,
            DateCompletion = instance.CompletedAt,
            IdNoeudCourant = instance.CurrentNodeName,
            NomDefinition = instance.DefinitionName,
            VersionDefinition = instance.DefinitionVersion,
            Statut = (int)instance.Status
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task<ProcessInstance?> GetProcessInstanceAsync(long processId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_ATTENTE, SIGNAL_ATTENTE, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
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
                DATE_ATTENTE = :DateAttente,
                SIGNAL_ATTENTE = :SignalAttente,
                DATE_DERNIERE_EXECUTION = :DateDerniereExecution,
                DATE_COMPLETION = :DateCompletion,
                ID_NOEUD_COURANT = :IdNoeudCourant,
                NOM_DEFINITION = :NomDefinition,
                VERSION_DEFINITION = :VersionDefinition,
                STATUT = :Statut
            WHERE ID_PROCESSUS = :IdProcessus";

        var parameters = new
        {
            IdAgregat = instance.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(instance.Variables),
            DateAttente = instance.InternalState.TryGetValue("WaitUntilDate", out var d) ? (DateTime?)d : null,
            SignalAttente = instance.InternalState.TryGetValue("WaitingForSignal", out var s) ? s?.ToString() : null,
            DateDerniereExecution = instance.LastExecutedAt,
            DateCompletion = instance.CompletedAt,
            IdNoeudCourant = instance.CurrentNodeName,
            NomDefinition = instance.DefinitionName,
            VersionDefinition = instance.DefinitionVersion,
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

    public async Task DeleteProcessInstanceAsync(long processId)
    {
        var sql = $@"DELETE FROM {_processContextTable} WHERE ID_PROCESSUS = :IdProcessus";
        await _connection.ExecuteAsync(sql, new { IdProcessus = processId });
    }

    public async Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_ATTENTE, SIGNAL_ATTENTE, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE DONNEES IS NOT NULL";

        var results = await _connection.QueryAsync<ProcessInstanceDto>(sql);
        var matches = new List<ProcessInstance>();

        foreach (var result in results)
        {
            var variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>();

            var allMatch = filtres.All(filtre =>
                variables.TryGetValue(filtre.NomVariable, out var valeur) &&
                filtre.Correspond(valeur));

            if (allMatch)
            {
                var instance = MapToInstance(result);
                instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.ID_PROCESSUS);
                matches.Add(instance);
            }
        }

        return matches;
    }

    public async Task<(NodeInstance History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId)
    {
        return await _historyRepository.GetByIdAsync(historyId);
    }

    public async Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeName)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_ATTENTE, SIGNAL_ATTENTE, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS_PARENT = :IdProcessusParent AND ID_NOEUD_PARENT = :IdNoeudParent";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessInstanceDto>(sql, new { IdProcessusParent = parentProcessId, IdNoeudParent = parentNodeName });

        if (result == null)
            return null;

        var instance = MapToInstance(result);
        instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.ID_PROCESSUS);
        return instance;
    }

    public async Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_ATTENTE, SIGNAL_ATTENTE, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS_PARENT = :IdProcessusParent";

        var results = await _connection.QueryAsync<ProcessInstanceDto>(sql, new { IdProcessusParent = parentProcessId });
        var children = new List<ProcessInstance>();

        foreach (var result in results)
        {
            var instance = MapToInstance(result);
            instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.ID_PROCESSUS);
            children.Add(instance);
        }

        return children;
    }

    private ProcessInstance MapToInstance(ProcessInstanceDto result)
    {
        var internalState = new Dictionary<string, object>();
        if (result.DATE_ATTENTE.HasValue)
            internalState["WaitUntilDate"] = result.DATE_ATTENTE.Value;
        if (!string.IsNullOrEmpty(result.SIGNAL_ATTENTE))
            internalState["WaitingForSignal"] = result.SIGNAL_ATTENTE;

        return new ProcessInstance(result.ID_PROCESSUS)
        {
            ParentProcessId = result.ID_PROCESSUS_PARENT,
            ParentNodeName = result.ID_NOEUD_PARENT,
            AggregateId = result.ID_AGREGAT,
            DefinitionName = result.NOM_DEFINITION,
            DefinitionVersion = result.VERSION_DEFINITION,
            Variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>(),
            InternalState = internalState,
            StartedAt = result.DATE_DEBUT,
            LastExecutedAt = result.DATE_DERNIERE_EXECUTION,
            CompletedAt = result.DATE_COMPLETION,
            CurrentNodeName = result.ID_NOEUD_COURANT,
            Status = (ProcessStatus)result.STATUT
        };
    }

    private class ProcessInstanceDto
    {
        public long ID_PROCESSUS { get; set; }
        public long? ID_PROCESSUS_PARENT { get; set; }
        public string? ID_NOEUD_PARENT { get; set; }
        public string? ID_AGREGAT { get; set; }
        public string DONNEES { get; set; } = string.Empty;
        public DateTime? DATE_ATTENTE { get; set; }
        public string? SIGNAL_ATTENTE { get; set; }
        public DateTime DATE_DEBUT { get; set; }
        public DateTime? DATE_DERNIERE_EXECUTION { get; set; }
        public DateTime? DATE_COMPLETION { get; set; }
        public string? ID_NOEUD_COURANT { get; set; }
        public string? NOM_DEFINITION { get; set; }
        public string? VERSION_DEFINITION { get; set; }
        public int STATUT { get; set; }
    }
}
