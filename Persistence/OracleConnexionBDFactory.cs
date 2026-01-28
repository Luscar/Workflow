namespace SimpleBPM.Persistence;

public class OracleConnexionBDFactory : IConnexionBDFactory
{
    private readonly string _connectionString;

    public OracleConnexionBDFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IConnexionBD CreateConnexion()
    {
        return new OracleConnexionBD(_connectionString);
    }
}
