using System.Data;
using Dapper;

namespace SimpleBPM.Persistence;

public class OracleHistoryRepository
{
    private readonly OracleConfiguration _config;
    private readonly IDbConnection _connection;
    private readonly string _historyTable;
    private readonly string _sequenceName;

    public OracleHistoryRepository(OracleConfiguration config, IDbConnection connection)
    {
        _config = config;
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _historyTable = _config.GetTableName("HISTORIQUE_EXECUTION_NOEUD");
        _sequenceName = _config.GetTableName("SEQ_HISTORIQUE");
    }

    private async Task<long> ObtenirSequenceAsync()
    {
        var sql = $"SELECT {_sequenceName}.NEXTVAL FROM DUAL";
        return await _connection.ExecuteScalarAsync<long>(sql);
    }

    public async Task InitializeDatabaseAsync()
    {
        var createTableSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE TABLE {_historyTable} (
                    NO_SEQ_NOEUD NUMBER(10) PRIMARY KEY,
                    NO_SEQ_PROCS NUMBER(10) NOT NULL,
                    ID_NOEUD VARCHAR2(255) NOT NULL,
                    TYPE_NOEUD NUMBER(10) NOT NULL,
                    DH_DEB DATE NOT NULL,
                    DH_FIN DATE NOT NULL,
                    IND_SUCCS NUMBER(1) NOT NULL,
                    MESS_ERR VARCHAR2(4000),
                    ID_NOEUD_SUIV VARCHAR2(255),
                    CONSTRAINT FK_{_config.TablePrefix}_HIST_PROC
                        FOREIGN KEY (NO_SEQ_PROCS)
                        REFERENCES {_config.GetTableName("PROCESS_CONTEXT")}(NO_SEQ_PROCS)
                        ON DELETE CASCADE
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_01_HISTORIQUE_EXECUTION_NOEUD ON {_historyTable}(NO_SEQ_PROCS)';
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_02_HISTORIQUE_EXECUTION_NOEUD ON {_historyTable}(ID_NOEUD)';
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_03_HISTORIQUE_EXECUTION_NOEUD ON {_historyTable}(DH_DEB)';
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
    }

    public async Task SaveHistoryAsync(long processId, NodeInstance history)
    {
        var sql = $@"
            INSERT INTO {_historyTable}
            (NO_SEQ_NOEUD, NO_SEQ_PROCS, ID_NOEUD, TYPE_NOEUD, DH_DEB, DH_FIN, IND_SUCCS, MESS_ERR, ID_NOEUD_SUIV)
            VALUES
            (:NoSeqNoed, :NoSeqProcs, :IdNoeud, :TypeNoeud, :DhDeb, :DhFin, :IndSuccs, :MessErr, :IdNoeudSuiv)";

        var parameters = new
        {
            NoSeqNoed = await ObtenirSequenceAsync(),
            NoSeqProcs = processId,
            IdNoeud = history.NodeId,
            TypeNoeud = (int)history.NodeType,
            DhDeb = history.StartedAt,
            DhFin = history.CompletedAt,
            IndSuccs = history.Success ? 1 : 0,
            MessErr = history.ErrorMessage,
            IdNoeudSuiv = history.NextNodeId
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task SaveHistoryBatchAsync(long processId, List<NodeInstance> histories)
    {
        if (histories.Count == 0) return;

        var sql = $@"
            INSERT INTO {_historyTable}
            (NO_SEQ_NOEUD, NO_SEQ_PROCS, ID_NOEUD, TYPE_NOEUD, DH_DEB, DH_FIN, IND_SUCCS, MESS_ERR, ID_NOEUD_SUIV)
            VALUES
            (:NoSeqNoed, :NoSeqProcs, :IdNoeud, :TypeNoeud, :DhDeb, :DhFin, :IndSuccs, :MessErr, :IdNoeudSuiv)";

        var parametersList = new List<object>();
        foreach (var history in histories)
        {
            parametersList.Add(new
            {
                NoSeqNoed = await ObtenirSequenceAsync(),
                NoSeqProcs = processId,
                IdNoeud = history.NodeId,
                TypeNoeud = (int)history.NodeType,
                DhDeb = history.StartedAt,
                DhFin = history.CompletedAt,
                IndSuccs = history.Success ? 1 : 0,
                MessErr = history.ErrorMessage,
                IdNoeudSuiv = history.NextNodeId
            });
        }

        await _connection.ExecuteAsync(sql, parametersList);
    }

    public async Task<List<NodeInstance>> GetHistoryAsync(long processId)
    {
        var sql = $@"
            SELECT ID_NOEUD, TYPE_NOEUD, DH_DEB, DH_FIN, IND_SUCCS, MESS_ERR, ID_NOEUD_SUIV
            FROM {_historyTable}
            WHERE NO_SEQ_PROCS = :NoSeqProcs
            ORDER BY DH_DEB";

        var results = await _connection.QueryAsync<NodeInstanceDto>(sql, new { NoSeqProcs = processId });

        var histories = new List<NodeInstance>();
        foreach (var result in results)
        {
            var history = new NodeInstance(
                result.ID_NOEUD,
                result.ID_NOEUD,
                (NodeType)result.TYPE_NOEUD
            );

            history.StartedAt = result.DH_DEB;
            history.Complete(
                result.IND_SUCCS == 1,
                result.MESS_ERR,
                result.ID_NOEUD_SUIV
            );
            history.CompletedAt = result.DH_FIN;

            histories.Add(history);
        }

        return histories;
    }

    public async Task<(NodeInstance History, long ProcessId)?> GetByIdAsync(long historyId)
    {
        var sql = $@"
            SELECT NO_SEQ_PROCS, ID_NOEUD, TYPE_NOEUD, DH_DEB, DH_FIN, IND_SUCCS, MESS_ERR, ID_NOEUD_SUIV
            FROM {_historyTable}
            WHERE NO_SEQ_NOEUD = :NoSeqNoed";

        var result = await _connection.QueryFirstOrDefaultAsync<NodeHistoryWithProcessDto>(sql, new { NoSeqNoed = historyId });

        if (result == null)
            return null;

        var history = new NodeInstance(
            result.ID_NOEUD,
            result.ID_NOEUD,
            (NodeType)result.TYPE_NOEUD
        );

        history.StartedAt = result.DH_DEB;
        history.Complete(
            result.IND_SUCCS == 1,
            result.MESS_ERR,
            result.ID_NOEUD_SUIV
        );
        history.CompletedAt = result.DH_FIN;

        return (history, result.NO_SEQ_PROCS);
    }

    private class NodeHistoryWithProcessDto
    {
        public long NO_SEQ_PROCS { get; set; }
        public string ID_NOEUD { get; set; } = string.Empty;
        public int TYPE_NOEUD { get; set; }
        public DateTime DH_DEB { get; set; }
        public DateTime DH_FIN { get; set; }
        public int IND_SUCCS { get; set; }
        public string? MESS_ERR { get; set; }
        public string? ID_NOEUD_SUIV { get; set; }
    }

    private class NodeInstanceDto
    {
        public string ID_NOEUD { get; set; } = string.Empty;
        public int TYPE_NOEUD { get; set; }
        public DateTime DH_DEB { get; set; }
        public DateTime DH_FIN { get; set; }
        public int IND_SUCCS { get; set; }
        public string? MESS_ERR { get; set; }
        public string? ID_NOEUD_SUIV { get; set; }
    }
}
