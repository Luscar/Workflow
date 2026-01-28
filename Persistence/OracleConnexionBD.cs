using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace SimpleBPM.Persistence;

public class OracleConnexionBD : IConnexionBD
{
    private readonly OracleConnection _connection;
    private bool _disposed;

    public Guid ConnexionID { get; }

    public IDbConnection Connexion => _connection;

    /// <summary>
    /// Toujours true car cette classe crée sa propre connexion.
    /// </summary>
    public bool OwnsConnection => true;

    public OracleConnexionBD(string connectionString)
    {
        ConnexionID = Guid.NewGuid();
        _connection = new OracleConnection(connectionString);
        _connection.Open();
    }

    public void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_connection != null && _connection.State != ConnectionState.Closed)
        {
            _connection.Close();
            _connection.Dispose();
        }
        _disposed = true;
    }
}
