using System.Data;
using Dapper;
using SimpleBPM.Definition;

namespace SimpleBPM.Persistence;

/// <summary>
/// Implémentation Oracle de <see cref="IDefinitionRepository"/>.
/// Sauvegarde les définitions de processus en JSON dans la table {PREFIX}_DEFINITION.
/// </summary>
public class OracleDefinitionRepository : IDefinitionRepository
{
    private readonly OracleConfiguration _config;
    private readonly IDbConnection _connection;
    private readonly string _definitionTable;

    public OracleDefinitionRepository(OracleConfiguration config, IDbConnection connection)
    {
        _config = config;
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _definitionTable = _config.GetTableName("DEFINITION");
    }

    /// <summary>
    /// Crée la table {PREFIX}_DEFINITION si elle n'existe pas déjà.
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        var createTableSql = $@"
            BEGIN
                EXECUTE IMMEDIATE 'CREATE TABLE {_definitionTable} (
                    ID_DEFIN VARCHAR2(255) NOT NULL,
                    VERSI_DEFIN VARCHAR2(50) NOT NULL,
                    DEFIN_JSON CLOB NOT NULL,
                    DH_CREAT DATE NOT NULL,
                    DH_MODIF DATE,
                    CONSTRAINT PK_{_config.TablePrefix}_DEFIN PRIMARY KEY (ID_DEFIN, VERSI_DEFIN)
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
    }

    public async Task SaveDefinitionAsync(ProcessDefinition definition)
    {
        var json = ProcessJsonLoader.ToJson(definition, indented: false);

        var countSql = $@"
            SELECT COUNT(*) FROM {_definitionTable}
            WHERE ID_DEFIN = :IdDefin AND VERSI_DEFIN = :VersiDefin";

        var exists = await _connection.ExecuteScalarAsync<int>(countSql, new
        {
            IdDefin = definition.Name,
            VersiDefin = definition.Version
        }) > 0;

        if (exists)
        {
            var updateSql = $@"
                UPDATE {_definitionTable}
                SET DEFIN_JSON = :DefinJson, DH_MODIF = :DhModif
                WHERE ID_DEFIN = :IdDefin AND VERSI_DEFIN = :VersiDefin";

            await _connection.ExecuteAsync(updateSql, new
            {
                DefinJson = json,
                DhModif = DateTime.UtcNow,
                IdDefin = definition.Name,
                VersiDefin = definition.Version
            });
        }
        else
        {
            var insertSql = $@"
                INSERT INTO {_definitionTable} (ID_DEFIN, VERSI_DEFIN, DEFIN_JSON, DH_CREAT)
                VALUES (:IdDefin, :VersiDefin, :DefinJson, :DhCreat)";

            await _connection.ExecuteAsync(insertSql, new
            {
                IdDefin = definition.Name,
                VersiDefin = definition.Version,
                DefinJson = json,
                DhCreat = DateTime.UtcNow
            });
        }
    }

    public async Task<ProcessDefinition?> GetDefinitionAsync(string name, string version)
    {
        var sql = $@"
            SELECT DEFIN_JSON FROM {_definitionTable}
            WHERE ID_DEFIN = :IdDefin AND VERSI_DEFIN = :VersiDefin";

        var json = await _connection.ExecuteScalarAsync<string>(sql, new
        {
            IdDefin = name,
            VersiDefin = version
        });

        return string.IsNullOrEmpty(json) ? null : ProcessJsonLoader.FromJson(json);
    }

    public async Task<List<ProcessDefinition>> GetAllDefinitionsAsync()
    {
        var sql = $@"
            SELECT DEFIN_JSON FROM {_definitionTable}
            ORDER BY ID_DEFIN, VERSI_DEFIN";

        var jsonList = await _connection.QueryAsync<string>(sql);

        return jsonList
            .Where(j => !string.IsNullOrEmpty(j))
            .Select(j => ProcessJsonLoader.FromJson(j))
            .ToList();
    }

    public async Task<List<string>> GetDefinitionVersionsAsync(string name)
    {
        var sql = $@"
            SELECT VERSI_DEFIN FROM {_definitionTable}
            WHERE ID_DEFIN = :IdDefin
            ORDER BY VERSI_DEFIN";

        var versions = await _connection.QueryAsync<string>(sql, new { IdDefin = name });
        return versions.ToList();
    }

    public async Task DeleteDefinitionAsync(string name, string version)
    {
        var sql = $@"
            DELETE FROM {_definitionTable}
            WHERE ID_DEFIN = :IdDefin AND VERSI_DEFIN = :VersiDefin";

        await _connection.ExecuteAsync(sql, new { IdDefin = name, VersiDefin = version });
    }
}
