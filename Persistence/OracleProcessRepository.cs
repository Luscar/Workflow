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
                    NO_SEQ_PROCS NUMBER(10) PRIMARY KEY,
                    NO_SEQ_PROCS_PARN NUMBER(10),
                    ID_NOEUD_PARN VARCHAR2(255),
                    ID_ENTI_AFFA VARCHAR2(255),
                    DONNEES CLOB,
                    DH_ATTENTE TIMESTAMP,
                    SIGNAL_ATTENTE VARCHAR2(255),
                    DH_DEB TIMESTAMP,
                    DH_DERN_EXEC TIMESTAMP,
                    DH_COMPL TIMESTAMP,
                    ID_NOEUD_COUR VARCHAR2(255),
                    NOM_DEFIN VARCHAR2(255),
                    VERSION_DEFIN VARCHAR2(50),
                    STAT_PROCS NUMBER(10),
                    CONSTRAINT CHK_{_config.TablePrefix}_STAT_PROCS CHECK (STAT_PROCS BETWEEN 0 AND 5)
                )';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN
                        NULL;
                    ELSE
                        RAISE;
                    END IF;
            END;";

        var addDhAttenteColumnSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'ALTER TABLE {_processContextTable} ADD (DH_ATTENTE TIMESTAMP)';
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
        await _connection.ExecuteAsync(addDhAttenteColumnSql);
        await _connection.ExecuteAsync(addSignalAttenteColumnSql);

        var createIndex01Sql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_01_PROCESS_CONTEXT ON {_processContextTable}(ID_ENTI_AFFA)';
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_02_PROCESS_CONTEXT ON {_processContextTable}(STAT_PROCS)';
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_03_PROCESS_CONTEXT ON {_processContextTable}(DH_DEB)';
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
            (NO_SEQ_PROCS, NO_SEQ_PROCS_PARN, ID_NOEUD_PARN, ID_ENTI_AFFA, DONNEES, DH_ATTENTE, SIGNAL_ATTENTE, DH_DEB, DH_DERN_EXEC, DH_COMPL, ID_NOEUD_COUR, NOM_DEFIN, VERSION_DEFIN, STAT_PROCS)
            VALUES
            (:NoSeqProcs, :NoSeqProcsParn, :IdNoeudParn, :IdEntiAffa, :Donnees, :DhAttente, :SignalAttente, :DhDeb, :DhDernExec, :DhCompl, :IdNoeudCour, :NomDefin, :VersionDefin, :StatProcs)";

        var parameters = new
        {
            NoSeqProcs = instance.ProcessId,
            NoSeqProcsParn = instance.ParentProcessId,
            IdNoeudParn = instance.ParentNodeName,
            IdEntiAffa = instance.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(instance.Variables),
            DhAttente = instance.WaitDate,
            SignalAttente = instance.ExpectedSignal,
            DhDeb = instance.StartedAt,
            DhDernExec = instance.LastExecutedAt,
            DhCompl = instance.CompletedAt,
            IdNoeudCour = instance.CurrentNodeName,
            NomDefin = instance.DefinitionName,
            VersionDefin = instance.DefinitionVersion,
            StatProcs = (int)instance.Status
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task<ProcessInstance?> GetProcessInstanceAsync(long processId)
    {
        var sql = $@"
            SELECT NO_SEQ_PROCS, NO_SEQ_PROCS_PARN, ID_NOEUD_PARN, ID_ENTI_AFFA, DONNEES, DH_ATTENTE, SIGNAL_ATTENTE, DH_DEB, DH_DERN_EXEC, DH_COMPL, ID_NOEUD_COUR, NOM_DEFIN, VERSION_DEFIN, STAT_PROCS
            FROM {_processContextTable}
            WHERE NO_SEQ_PROCS = :NoSeqProcs";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessInstanceDto>(sql, new { NoSeqProcs = processId });

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
            SET ID_ENTI_AFFA = :IdEntiAffa,
                DONNEES = :Donnees,
                DH_ATTENTE = :DhAttente,
                SIGNAL_ATTENTE = :SignalAttente,
                DH_DERN_EXEC = :DhDernExec,
                DH_COMPL = :DhCompl,
                ID_NOEUD_COUR = :IdNoeudCour,
                NOM_DEFIN = :NomDefin,
                VERSION_DEFIN = :VersionDefin,
                STAT_PROCS = :StatProcs
            WHERE NO_SEQ_PROCS = :NoSeqProcs";

        var parameters = new
        {
            IdEntiAffa = instance.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(instance.Variables),
            DhAttente = instance.WaitDate,
            SignalAttente = instance.ExpectedSignal,
            DhDernExec = instance.LastExecutedAt,
            DhCompl = instance.CompletedAt,
            IdNoeudCour = instance.CurrentNodeName,
            NomDefin = instance.DefinitionName,
            VersionDefin = instance.DefinitionVersion,
            StatProcs = (int)instance.Status,
            NoSeqProcs = instance.ProcessId
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
        var sql = $@"DELETE FROM {_processContextTable} WHERE NO_SEQ_PROCS = :NoSeqProcs";
        await _connection.ExecuteAsync(sql, new { NoSeqProcs = processId });
    }

    public async Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres)
    {
        var sql = $@"
            SELECT NO_SEQ_PROCS, NO_SEQ_PROCS_PARN, ID_NOEUD_PARN, ID_ENTI_AFFA, DONNEES, DH_ATTENTE, SIGNAL_ATTENTE, DH_DEB, DH_DERN_EXEC, DH_COMPL, ID_NOEUD_COUR, NOM_DEFIN, VERSION_DEFIN, STAT_PROCS
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
                instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.NO_SEQ_PROCS);
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
            SELECT NO_SEQ_PROCS, NO_SEQ_PROCS_PARN, ID_NOEUD_PARN, ID_ENTI_AFFA, DONNEES, DH_ATTENTE, SIGNAL_ATTENTE, DH_DEB, DH_DERN_EXEC, DH_COMPL, ID_NOEUD_COUR, NOM_DEFIN, VERSION_DEFIN, STAT_PROCS
            FROM {_processContextTable}
            WHERE NO_SEQ_PROCS_PARN = :NoSeqProcsParn AND ID_NOEUD_PARN = :IdNoeudParn";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessInstanceDto>(sql, new { NoSeqProcsParn = parentProcessId, IdNoeudParn = parentNodeName });

        if (result == null)
            return null;

        var instance = MapToInstance(result);
        instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.NO_SEQ_PROCS);
        return instance;
    }

    public async Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId)
    {
        var sql = $@"
            SELECT NO_SEQ_PROCS, NO_SEQ_PROCS_PARN, ID_NOEUD_PARN, ID_ENTI_AFFA, DONNEES, DH_ATTENTE, SIGNAL_ATTENTE, DH_DEB, DH_DERN_EXEC, DH_COMPL, ID_NOEUD_COUR, NOM_DEFIN, VERSION_DEFIN, STAT_PROCS
            FROM {_processContextTable}
            WHERE NO_SEQ_PROCS_PARN = :NoSeqProcsParn";

        var results = await _connection.QueryAsync<ProcessInstanceDto>(sql, new { NoSeqProcsParn = parentProcessId });
        var children = new List<ProcessInstance>();

        foreach (var result in results)
        {
            var instance = MapToInstance(result);
            instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.NO_SEQ_PROCS);
            children.Add(instance);
        }

        return children;
    }

    private ProcessInstance MapToInstance(ProcessInstanceDto result)
    {
        return new ProcessInstance(result.NO_SEQ_PROCS)
        {
            ParentProcessId = result.NO_SEQ_PROCS_PARN,
            ParentNodeName = result.ID_NOEUD_PARN,
            AggregateId = result.ID_ENTI_AFFA,
            DefinitionName = result.NOM_DEFIN,
            DefinitionVersion = result.VERSION_DEFIN,
            Variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>(),
            WaitDate = result.DH_ATTENTE,
            ExpectedSignal = result.SIGNAL_ATTENTE,
            StartedAt = result.DH_DEB,
            LastExecutedAt = result.DH_DERN_EXEC,
            CompletedAt = result.DH_COMPL,
            CurrentNodeName = result.ID_NOEUD_COUR,
            Status = (ProcessStatus)result.STAT_PROCS
        };
    }

    private class ProcessInstanceDto
    {
        public long NO_SEQ_PROCS { get; set; }
        public long? NO_SEQ_PROCS_PARN { get; set; }
        public string? ID_NOEUD_PARN { get; set; }
        public string? ID_ENTI_AFFA { get; set; }
        public string DONNEES { get; set; } = string.Empty;
        public DateTime? DH_ATTENTE { get; set; }
        public string? SIGNAL_ATTENTE { get; set; }
        public DateTime DH_DEB { get; set; }
        public DateTime? DH_DERN_EXEC { get; set; }
        public DateTime? DH_COMPL { get; set; }
        public string? ID_NOEUD_COUR { get; set; }
        public string? NOM_DEFIN { get; set; }
        public string? VERSION_DEFIN { get; set; }
        public int STAT_PROCS { get; set; }
    }
}
