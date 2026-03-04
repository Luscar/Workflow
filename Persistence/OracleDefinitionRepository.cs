using System.Data;
using Dapper;
using SimpleBPM.Definition;

namespace SimpleBPM.Persistence;

/// <summary>
/// Implémentation Oracle de <see cref="IDefinitionRepository"/>.
/// Les définitions sont sérialisées en JSON via <see cref="ProcessJsonLoader"/>
/// et stockées dans la table <c>{PREFIX}_DEFINITION_BANQUE</c>.
/// </summary>
public class OracleDefinitionRepository : IDefinitionRepository
{
    private readonly OracleConfiguration _config;
    private readonly IDbConnection _connection;
    private readonly string _table;

    public OracleDefinitionRepository(OracleConfiguration config, IDbConnection connection)
    {
        _config = config;
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _table = _config.GetTableName("DEFINITION_BANQUE");
    }

    public async Task InitializeDatabaseAsync()
    {
        var createSequenceSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE SEQUENCE {_config.GetTableName("SEQ_DEFINITION")} START WITH 1 INCREMENT BY 1 NOCACHE';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
            END;";

        var createTableSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE TABLE {_table} (
                    ID_DEFIN VARCHAR2(255) NOT NULL,
                    VERSI_DEFIN VARCHAR2(50) NOT NULL,
                    CONTENU_DEFIN CLOB NOT NULL,
                    DH_CREATION DATE NOT NULL,
                    CONSTRAINT PK_{_config.TablePrefix}_DEFIN_BANQUE PRIMARY KEY (ID_DEFIN, VERSI_DEFIN)
                )';
            EXCEPTION
                WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
            END;";

        await _connection.ExecuteAsync(createSequenceSql);
        await _connection.ExecuteAsync(createTableSql);
    }

    public async Task SaveDefinitionAsync(ProcessDefinition definition)
    {
        var json = ProcessJsonLoader.ToJson(definition);

        var mergeSql = $@"
            MERGE INTO {_table} target
            USING (SELECT :IdDefin AS ID_DEFIN, :VersiDefin AS VERSI_DEFIN FROM DUAL) source
            ON (target.ID_DEFIN = source.ID_DEFIN AND target.VERSI_DEFIN = source.VERSI_DEFIN)
            WHEN MATCHED THEN
                UPDATE SET CONTENU_DEFIN = :ContenuDefin, DH_CREATION = :DhCreation
            WHEN NOT MATCHED THEN
                INSERT (ID_DEFIN, VERSI_DEFIN, CONTENU_DEFIN, DH_CREATION)
                VALUES (:IdDefin, :VersiDefin, :ContenuDefin, :DhCreation)";

        await _connection.ExecuteAsync(mergeSql, new
        {
            IdDefin = definition.Name,
            VersiDefin = definition.Version,
            ContenuDefin = json,
            DhCreation = DateTime.UtcNow
        });
    }

    public async Task<ProcessDefinition?> GetDefinitionAsync(string name, string version)
    {
        var sql = $@"
            SELECT CONTENU_DEFIN FROM {_table}
            WHERE ID_DEFIN = :IdDefin AND VERSI_DEFIN = :VersiDefin";

        var json = await _connection.ExecuteScalarAsync<string>(sql, new { IdDefin = name, VersiDefin = version });
        return json == null ? null : ProcessJsonLoader.FromJson(json);
    }

    public async Task<List<ProcessDefinition>> GetDefinitionsByNameAsync(string name)
    {
        var sql = $@"
            SELECT CONTENU_DEFIN FROM {_table}
            WHERE ID_DEFIN = :IdDefin
            ORDER BY DH_CREATION DESC";

        var rows = await _connection.QueryAsync<string>(sql, new { IdDefin = name });
        return rows.Select(ProcessJsonLoader.FromJson).ToList();
    }

    public async Task<List<ProcessDefinition>> GetAllDefinitionsAsync()
    {
        var sql = $@"
            SELECT CONTENU_DEFIN FROM {_table}
            ORDER BY ID_DEFIN, DH_CREATION DESC";

        var rows = await _connection.QueryAsync<string>(sql);
        return rows.Select(ProcessJsonLoader.FromJson).ToList();
    }

    public async Task DeleteDefinitionAsync(string name, string version)
    {
        var sql = $@"DELETE FROM {_table} WHERE ID_DEFIN = :IdDefin AND VERSI_DEFIN = :VersiDefin";
        await _connection.ExecuteAsync(sql, new { IdDefin = name, VersiDefin = version });
    }
}
