using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace SimpleBPM.Persistence;

public class OracleConnexionBD : IConnexionBD
{
    private readonly OracleConnection _connection;

    public Guid ConnexionID { get; }
    
    public IDbConnection Connexion => _connection;

    public OracleConnexionBD(string connectionString)
    {
        ConnexionID = Guid.NewGuid();
        _connection = new OracleConnection(connectionString);
        _connection.Open();
    }

    public void Close()
    {
        if (_connection != null && _connection.State != ConnectionState.Closed)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
