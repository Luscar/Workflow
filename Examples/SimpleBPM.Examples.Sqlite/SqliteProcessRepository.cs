using System.Data;
using System.Text.Json;
using Dapper;
using SimpleBPM.Persistence;

namespace SimpleBPM.Examples.Sqlite;

public class SqliteProcessRepository : IProcessRepository
{
    private readonly SqliteConfiguration _config;
    private readonly IDbConnection _connection;
    private readonly string _processContextTable;
    private readonly SqliteHistoryRepository _historyRepository;

    public SqliteProcessRepository(SqliteConfiguration config, IDbConnection connection)
    {
        _config = config;
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _processContextTable = _config.GetTableName("PROCESS_CONTEXT");
        _historyRepository = new SqliteHistoryRepository(config, connection);
    }

    public Task<long> ObtenirSequenceAsync(string nomSequence)
    {
        // SQLite uses AUTOINCREMENT via INTEGER PRIMARY KEY, so we generate IDs
        // by inserting into a lightweight sequence-emulation table.
        var seqTable = _config.GetTableName("SEQUENCES");
        var sql = $@"
            INSERT INTO {seqTable} (NOM) VALUES (@Nom);
            SELECT last_insert_rowid();";
        return _connection.ExecuteScalarAsync<long>(sql, new { Nom = nomSequence });
    }

    public async Task InitializeDatabaseAsync()
    {
        // Sequence emulation table
        var seqTable = _config.GetTableName("SEQUENCES");
        await _connection.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS {seqTable} (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                NOM TEXT NOT NULL
            )");

        // Process context table
        await _connection.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS {_processContextTable} (
                ID_PROCESSUS INTEGER PRIMARY KEY,
                ID_PROCESSUS_PARENT INTEGER,
                ID_NOEUD_PARENT TEXT,
                ID_AGREGAT TEXT,
                DONNEES TEXT,
                DATE_DEBUT TEXT NOT NULL,
                DATE_DERNIERE_EXECUTION TEXT,
                DATE_COMPLETION TEXT,
                ID_NOEUD_COURANT TEXT,
                NOM_DEFINITION TEXT,
                VERSION_DEFINITION TEXT,
                STATUT INTEGER NOT NULL CHECK (STATUT BETWEEN 0 AND 5)
            )");

        await _connection.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS IX_{_config.TablePrefix}_AGREGAT ON {_processContextTable}(ID_AGREGAT)");
        await _connection.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS IX_{_config.TablePrefix}_STATUT ON {_processContextTable}(STATUT)");
        await _connection.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS IX_{_config.TablePrefix}_DATE ON {_processContextTable}(DATE_DEBUT)");

        await _historyRepository.InitializeDatabaseAsync();
    }

    public async Task SaveProcessInstanceAsync(ProcessInstance instance)
    {
        var sql = $@"
            INSERT INTO {_processContextTable}
            (ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT)
            VALUES
            (@IdProcessus, @IdProcessusParent, @IdNoeudParent, @IdAgregat, @Donnees, @DateDebut, @DateDerniereExecution, @DateCompletion, @IdNoeudCourant, @NomDefinition, @VersionDefinition, @Statut)";

        var parameters = new
        {
            IdProcessus = instance.ProcessId,
            IdProcessusParent = instance.ParentProcessId,
            IdNoeudParent = instance.ParentNodeId,
            IdAgregat = instance.AggregateId,
            Donnees = JsonSerializer.Serialize(instance.Variables),
            DateDebut = instance.StartedAt.ToString("o"),
            DateDerniereExecution = instance.LastExecutedAt?.ToString("o"),
            DateCompletion = instance.CompletedAt?.ToString("o"),
            IdNoeudCourant = instance.CurrentNodeId,
            NomDefinition = instance.DefinitionName,
            VersionDefinition = instance.DefinitionVersion,
            Statut = (int)instance.Status
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task<ProcessInstance?> GetProcessInstanceAsync(long processId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS = @IdProcessus";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessInstanceDto>(sql, new { IdProcessus = processId });

        if (result == null)
            return null;

        var instance = MapToInstance(result);
        instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(processId);
        return instance;
    }

    public async Task UpdateProcessInstanceAsync(ProcessInstance instance)
    {
        var sql = $@"
            UPDATE {_processContextTable}
            SET ID_AGREGAT = @IdAgregat,
                DONNEES = @Donnees,
                DATE_DERNIERE_EXECUTION = @DateDerniereExecution,
                DATE_COMPLETION = @DateCompletion,
                ID_NOEUD_COURANT = @IdNoeudCourant,
                NOM_DEFINITION = @NomDefinition,
                VERSION_DEFINITION = @VersionDefinition,
                STATUT = @Statut
            WHERE ID_PROCESSUS = @IdProcessus";

        var parameters = new
        {
            IdAgregat = instance.AggregateId,
            Donnees = JsonSerializer.Serialize(instance.Variables),
            DateDerniereExecution = instance.LastExecutedAt?.ToString("o"),
            DateCompletion = instance.CompletedAt?.ToString("o"),
            IdNoeudCourant = instance.CurrentNodeId,
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
        await _connection.ExecuteAsync(
            $"DELETE FROM {_processContextTable} WHERE ID_PROCESSUS = @IdProcessus",
            new { IdProcessus = processId });
    }

    public async Task<List<ProcessInstance>> SearchByVariableAsync(List<FiltreVariable> filtres)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE DONNEES IS NOT NULL";

        var results = await _connection.QueryAsync<ProcessInstanceDto>(sql);
        var matches = new List<ProcessInstance>();

        foreach (var result in results)
        {
            var variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>();

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

    public async Task<(NodeExecutionHistory History, long ProcessId)?> GetNodeHistoryByIdAsync(long historyId)
    {
        return await _historyRepository.GetByIdAsync(historyId);
    }

    public async Task<ProcessInstance?> GetChildProcessAsync(long parentProcessId, string parentNodeId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS_PARENT = @IdProcessusParent AND ID_NOEUD_PARENT = @IdNoeudParent";

        var result = await _connection.QueryFirstOrDefaultAsync<ProcessInstanceDto>(sql,
            new { IdProcessusParent = parentProcessId, IdNoeudParent = parentNodeId });

        if (result == null)
            return null;

        var instance = MapToInstance(result);
        instance.ExecutionHistory = await _historyRepository.GetHistoryAsync(result.ID_PROCESSUS);
        return instance;
    }

    public async Task<List<ProcessInstance>> GetChildrenAsync(long parentProcessId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_PROCESSUS_PARENT, ID_NOEUD_PARENT, ID_AGREGAT, DONNEES, DATE_DEBUT, DATE_DERNIERE_EXECUTION, DATE_COMPLETION, ID_NOEUD_COURANT, NOM_DEFINITION, VERSION_DEFINITION, STATUT
            FROM {_processContextTable}
            WHERE ID_PROCESSUS_PARENT = @IdProcessusParent";

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

    private static ProcessInstance MapToInstance(ProcessInstanceDto result)
    {
        return new ProcessInstance(result.ID_PROCESSUS)
        {
            ParentProcessId = result.ID_PROCESSUS_PARENT,
            ParentNodeId = result.ID_NOEUD_PARENT,
            AggregateId = result.ID_AGREGAT,
            DefinitionName = result.NOM_DEFINITION,
            DefinitionVersion = result.VERSION_DEFINITION,
            Variables = string.IsNullOrEmpty(result.DONNEES)
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(result.DONNEES) ?? new Dictionary<string, object>(),
            StartedAt = DateTime.Parse(result.DATE_DEBUT),
            LastExecutedAt = string.IsNullOrEmpty(result.DATE_DERNIERE_EXECUTION) ? null : DateTime.Parse(result.DATE_DERNIERE_EXECUTION),
            CompletedAt = string.IsNullOrEmpty(result.DATE_COMPLETION) ? null : DateTime.Parse(result.DATE_COMPLETION),
            CurrentNodeId = result.ID_NOEUD_COURANT,
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
        public string DATE_DEBUT { get; set; } = string.Empty;
        public string? DATE_DERNIERE_EXECUTION { get; set; }
        public string? DATE_COMPLETION { get; set; }
        public string? ID_NOEUD_COURANT { get; set; }
        public string? NOM_DEFINITION { get; set; }
        public string? VERSION_DEFINITION { get; set; }
        public int STATUT { get; set; }
    }
}
