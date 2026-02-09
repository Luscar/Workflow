using System.Data;
using Dapper;

namespace SimpleBPM.Examples.Sqlite;

public class SqliteHistoryRepository
{
    private readonly IDbConnection _connection;
    private readonly string _historyTable;

    public SqliteHistoryRepository(SqliteConfiguration config, IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _historyTable = config.GetTableName("HISTORIQUE_EXECUTION_NOEUD");
    }

    public async Task InitializeDatabaseAsync()
    {
        var createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {_historyTable} (
                ID_HISTORIQUE INTEGER PRIMARY KEY AUTOINCREMENT,
                ID_PROCESSUS INTEGER NOT NULL,
                ID_NOEUD TEXT NOT NULL,
                NOM_NOEUD TEXT,
                TYPE_NOEUD INTEGER NOT NULL,
                DATE_DEBUT TEXT NOT NULL,
                DATE_FIN TEXT NOT NULL,
                DUREE_MS INTEGER NOT NULL,
                SUCCES INTEGER NOT NULL,
                MESSAGE_ERREUR TEXT,
                ID_NOEUD_SUIVANT TEXT,
                FOREIGN KEY (ID_PROCESSUS) REFERENCES {_historyTable.Replace("HISTORIQUE_EXECUTION_NOEUD", "PROCESS_CONTEXT")}(ID_PROCESSUS)
                    ON DELETE CASCADE
            )";

        await _connection.ExecuteAsync(createTableSql);

        await _connection.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS IX_{_historyTable}_PROCESSUS ON {_historyTable}(ID_PROCESSUS)");
        await _connection.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS IX_{_historyTable}_NOEUD ON {_historyTable}(ID_NOEUD)");
        await _connection.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS IX_{_historyTable}_DATE ON {_historyTable}(DATE_DEBUT)");
    }

    public async Task SaveHistoryBatchAsync(long processId, List<NodeExecutionHistory> histories)
    {
        if (histories.Count == 0) return;

        var sql = $@"
            INSERT INTO {_historyTable}
            (ID_PROCESSUS, ID_NOEUD, NOM_NOEUD, TYPE_NOEUD, DATE_DEBUT, DATE_FIN, DUREE_MS, SUCCES, MESSAGE_ERREUR, ID_NOEUD_SUIVANT)
            VALUES
            (@IdProcessus, @IdNoeud, @NomNoeud, @TypeNoeud, @DateDebut, @DateFin, @DureeMs, @Succes, @MessageErreur, @IdNoeudSuivant)";

        var parametersList = histories.Select(history => new
        {
            IdProcessus = processId,
            IdNoeud = history.NodeId,
            NomNoeud = history.NodeName,
            TypeNoeud = (int)history.NodeType,
            DateDebut = history.StartedAt.ToString("o"),
            DateFin = history.CompletedAt.ToString("o"),
            DureeMs = (long)history.Duration.TotalMilliseconds,
            Succes = history.Success ? 1 : 0,
            MessageErreur = history.ErrorMessage,
            IdNoeudSuivant = history.NextNodeId
        }).ToArray();

        await _connection.ExecuteAsync(sql, parametersList);
    }

    public async Task<List<NodeExecutionHistory>> GetHistoryAsync(long processId)
    {
        var sql = $@"
            SELECT ID_NOEUD, NOM_NOEUD, TYPE_NOEUD, DATE_DEBUT, DATE_FIN, SUCCES, MESSAGE_ERREUR, ID_NOEUD_SUIVANT
            FROM {_historyTable}
            WHERE ID_PROCESSUS = @IdProcessus
            ORDER BY DATE_DEBUT";

        var results = await _connection.QueryAsync<NodeExecutionHistoryDto>(sql, new { IdProcessus = processId });

        var histories = new List<NodeExecutionHistory>();
        foreach (var result in results)
        {
            var history = new NodeExecutionHistory(
                result.ID_NOEUD,
                result.NOM_NOEUD ?? string.Empty,
                (NodeType)result.TYPE_NOEUD
            );

            history.StartedAt = DateTime.Parse(result.DATE_DEBUT);
            history.Complete(
                result.SUCCES == 1,
                result.MESSAGE_ERREUR,
                result.ID_NOEUD_SUIVANT
            );
            history.CompletedAt = DateTime.Parse(result.DATE_FIN);

            histories.Add(history);
        }

        return histories;
    }

    public async Task<(NodeExecutionHistory History, long ProcessId)?> GetByIdAsync(long historyId)
    {
        var sql = $@"
            SELECT ID_PROCESSUS, ID_NOEUD, NOM_NOEUD, TYPE_NOEUD, DATE_DEBUT, DATE_FIN, SUCCES, MESSAGE_ERREUR, ID_NOEUD_SUIVANT
            FROM {_historyTable}
            WHERE ID_HISTORIQUE = @IdHistorique";

        var result = await _connection.QueryFirstOrDefaultAsync<NodeHistoryWithProcessDto>(sql, new { IdHistorique = historyId });

        if (result == null)
            return null;

        var history = new NodeExecutionHistory(
            result.ID_NOEUD,
            result.NOM_NOEUD ?? string.Empty,
            (NodeType)result.TYPE_NOEUD
        );

        history.StartedAt = DateTime.Parse(result.DATE_DEBUT);
        history.Complete(
            result.SUCCES == 1,
            result.MESSAGE_ERREUR,
            result.ID_NOEUD_SUIVANT
        );
        history.CompletedAt = DateTime.Parse(result.DATE_FIN);

        return (history, result.ID_PROCESSUS);
    }

    private class NodeHistoryWithProcessDto
    {
        public long ID_PROCESSUS { get; set; }
        public string ID_NOEUD { get; set; } = string.Empty;
        public string? NOM_NOEUD { get; set; }
        public int TYPE_NOEUD { get; set; }
        public string DATE_DEBUT { get; set; } = string.Empty;
        public string DATE_FIN { get; set; } = string.Empty;
        public int SUCCES { get; set; }
        public string? MESSAGE_ERREUR { get; set; }
        public string? ID_NOEUD_SUIVANT { get; set; }
    }

    private class NodeExecutionHistoryDto
    {
        public string ID_NOEUD { get; set; } = string.Empty;
        public string? NOM_NOEUD { get; set; }
        public int TYPE_NOEUD { get; set; }
        public string DATE_DEBUT { get; set; } = string.Empty;
        public string DATE_FIN { get; set; } = string.Empty;
        public int SUCCES { get; set; }
        public string? MESSAGE_ERREUR { get; set; }
        public string? ID_NOEUD_SUIVANT { get; set; }
    }
}
