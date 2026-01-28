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

    public async Task SaveProcessContextAsync(ProcessContext context)
    {
        var sql = $@"
            INSERT INTO {_processContextTable}
            (ID_PROCESSUS, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, STATUT)
            VALUES
            (:IdProcessus, :IdAgregat, :Donnees, :DateDebut, :DateDerniereExecution, :DateCompletion, :IdNoeudCourant, :Statut)";

        var parameters = new
        {
            IdProcessus = context.ProcessId,
            IdAgregat = context.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(context.Data),
            DateDebut = context.StartedAt,
            DateDerniereExecution = context.LastExecutedAt,
            DateCompletion = context.CompletedAt,
            IdNoeudCourant = context.CurrentNodeId,
            Statut = (int)context.Status
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task<ProcessContext?> GetProcessContextAsync(string processId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS = :IdProcessus";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessContextDto>(sql, new { IdProcessus = processId });

        if (result == null)
        {
            return null;
        }

        var context = new ProcessContext(result.ID_PROCESSUS)
        {
            AggregateId = result.ID_AGREGAT,
            Data = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>(),
            StartedAt = result.DATE_DEBUT,
            LastExecutedAt = result.DATE_DERNIERE_EXECUTION,
            CompletedAt = result.DATE_COMPLETION,
            CurrentNodeId = result.ID_NOEUD_COURANT,
            Status = (ProcessStatus)result.STATUT
        };

        context.ExecutionHistory = await _historyRepository.GetHistoryAsync(processId);

        return context;
    }

    public async Task UpdateProcessContextAsync(ProcessContext context)
    {
        var sql = $@"
            UPDATE {_processContextTable}
            SET ID_AGREGAT = :IdAgregat,
                DONNEES = :Donnees,
                DATE_DERNIERE_EXECUTION = :DateDerniereExecution,
                DATE_COMPLETION = :DateCompletion,
                ID_NOEUD_COURANT = :IdNoeudCourant,
                STATUT = :Statut
            WHERE ID_PROCESSUS = :IdProcessus";

        var parameters = new
        {
            IdAgregat = context.AggregateId,
            Donnees = System.Text.Json.JsonSerializer.Serialize(context.Data),
            DateDerniereExecution = context.LastExecutedAt,
            DateCompletion = context.CompletedAt,
            IdNoeudCourant = context.CurrentNodeId,
            Statut = (int)context.Status,
            IdProcessus = context.ProcessId
        };

        await _connection.ExecuteAsync(sql, parameters);

        var existingHistory = await _historyRepository.GetHistoryAsync(context.ProcessId);
        var newHistories = context.ExecutionHistory.Skip(existingHistory.Count).ToList();

        if (newHistories.Count > 0)
        {
            await _historyRepository.SaveHistoryBatchAsync(context.ProcessId, newHistories);
        }
    }

    public async Task DeleteProcessContextAsync(string processId)
    {
        var sql = $@"DELETE FROM {_processContextTable} WHERE ID_PROCESSUS = :IdProcessus";
        await _connection.ExecuteAsync(sql, new { IdProcessus = processId });
    }

    private class ProcessContextDto
    {
        public string ID_PROCESSUS { get; set; } = string.Empty;
        public string? ID_AGREGAT { get; set; }
        public string DONNEES { get; set; } = string.Empty;
        public DateTime DATE_DEBUT { get; set; }
        public DateTime? DATE_DERNIERE_EXECUTION { get; set; }
        public DateTime? DATE_COMPLETION { get; set; }
        public string? ID_NOEUD_COURANT { get; set; }
        public int STATUT { get; set; }
    }
}
