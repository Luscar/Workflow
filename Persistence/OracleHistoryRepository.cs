using System.Data;
using Dapper;

namespace SimpleBPM.Persistence;

public class OracleHistoryRepository
{
    private readonly OracleConfiguration _config;
    private readonly IDbConnection _connection;
    private readonly string _historyTable;

    public OracleHistoryRepository(OracleConfiguration config, IDbConnection connection)
    {
        _config = config;
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _historyTable = _config.GetTableName("HISTORIQUE_EXECUTION_NOEUD");
    }

    public async Task InitializeDatabaseAsync()
    {
        var createTableSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE TABLE {_historyTable} (
                    ID_HISTORIQUE VARCHAR2(50) PRIMARY KEY,
                    ID_PROCESSUS VARCHAR2(255) NOT NULL,
                    ID_NOEUD VARCHAR2(255) NOT NULL,
                    NOM_NOEUD VARCHAR2(500),
                    TYPE_NOEUD NUMBER(10) NOT NULL,
                    DATE_DEBUT TIMESTAMP NOT NULL,
                    DATE_FIN TIMESTAMP NOT NULL,
                    DUREE_MS NUMBER(19) NOT NULL,
                    SUCCES NUMBER(1) NOT NULL,
                    MESSAGE_ERREUR VARCHAR2(4000),
                    ID_NOEUD_SUIVANT VARCHAR2(255),
                    CONSTRAINT FK_{_config.TablePrefix}_HIST_PROC
                        FOREIGN KEY (ID_PROCESSUS)
                        REFERENCES {_config.GetTableName("PROCESS_CONTEXT")}(ID_PROCESSUS)
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_01_HISTORIQUE_EXECUTION_NOEUD ON {_historyTable}(ID_PROCESSUS)';
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
                EXECUTE IMMEDIATE 'CREATE INDEX IX_{_config.TablePrefix}_03_HISTORIQUE_EXECUTION_NOEUD ON {_historyTable}(DATE_DEBUT)';
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

    public async Task SaveHistoryAsync(string processId, NodeExecutionHistory history)
    {
        var sql = $@"
            INSERT INTO {_historyTable}
            (ID_HISTORIQUE, ID_PROCESSUS, ID_NOEUD, NOM_NOEUD, TYPE_NOEUD, DATE_DEBUT, DATE_FIN, DUREE_MS, SUCCES, MESSAGE_ERREUR, ID_NOEUD_SUIVANT)
            VALUES
            (:IdHistorique, :IdProcessus, :IdNoeud, :NomNoeud, :TypeNoeud, :DateDebut, :DateFin, :DureeMs, :Succes, :MessageErreur, :IdNoeudSuivant)";

        var parameters = new
        {
            IdHistorique = Guid.NewGuid().ToString(),
            IdProcessus = processId,
            IdNoeud = history.NodeId,
            NomNoeud = history.NodeName,
            TypeNoeud = (int)history.NodeType,
            DateDebut = history.StartedAt,
            DateFin = history.CompletedAt,
            DureeMs = (long)history.Duration.TotalMilliseconds,
            Succes = history.Success ? 1 : 0,
            MessageErreur = history.ErrorMessage,
            IdNoeudSuivant = history.NextNodeId
        };

        await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task SaveHistoryBatchAsync(string processId, List<NodeExecutionHistory> histories)
    {
        if (histories.Count == 0) return;

        var sql = $@"
            INSERT INTO {_historyTable}
            (ID_HISTORIQUE, ID_PROCESSUS, ID_NOEUD, NOM_NOEUD, TYPE_NOEUD, DATE_DEBUT, DATE_FIN, DUREE_MS, SUCCES, MESSAGE_ERREUR, ID_NOEUD_SUIVANT)
            VALUES
            (:IdHistorique, :IdProcessus, :IdNoeud, :NomNoeud, :TypeNoeud, :DateDebut, :DateFin, :DureeMs, :Succes, :MessageErreur, :IdNoeudSuivant)";

        var parametersList = histories.Select(history => new
        {
            IdHistorique = Guid.NewGuid().ToString(),
            IdProcessus = processId,
            IdNoeud = history.NodeId,
            NomNoeud = history.NodeName,
            TypeNoeud = (int)history.NodeType,
            DateDebut = history.StartedAt,
            DateFin = history.CompletedAt,
            DureeMs = (long)history.Duration.TotalMilliseconds,
            Succes = history.Success ? 1 : 0,
            MessageErreur = history.ErrorMessage,
            IdNoeudSuivant = history.NextNodeId
        }).ToList();

        await _connection.ExecuteAsync(sql, parametersList);
    }

    public async Task<List<NodeExecutionHistory>> GetHistoryAsync(string processId)
    {
        var sql = $@"
            SELECT ID_NOEUD, NOM_NOEUD, TYPE_NOEUD, DATE_DEBUT, DATE_FIN, SUCCES, MESSAGE_ERREUR, ID_NOEUD_SUIVANT
            FROM {_historyTable}
            WHERE ID_PROCESSUS = :IdProcessus
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

            history.StartedAt = result.DATE_DEBUT;
            history.Complete(
                result.SUCCES == 1,
                result.MESSAGE_ERREUR,
                result.ID_NOEUD_SUIVANT
            );
            history.CompletedAt = result.DATE_FIN;

            histories.Add(history);
        }

        return histories;
    }

    private class NodeExecutionHistoryDto
    {
        public string ID_NOEUD { get; set; } = string.Empty;
        public string? NOM_NOEUD { get; set; }
        public int TYPE_NOEUD { get; set; }
        public DateTime DATE_DEBUT { get; set; }
        public DateTime DATE_FIN { get; set; }
        public int SUCCES { get; set; }
        public string? MESSAGE_ERREUR { get; set; }
        public string? ID_NOEUD_SUIVANT { get; set; }
    }
}
