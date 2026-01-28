using System.Data;

namespace SimpleBPM.Persistence;

/// <summary>
/// Wrapper pour une connexion externe injectée par le client.
/// La connexion n'est PAS fermée lors du Dispose/Close car elle est gérée par le client.
/// </summary>
public class ExternalConnexionBD : IConnexionBD
{
    private readonly IDbConnection _connection;

    public Guid ConnexionID { get; }

    public IDbConnection Connexion => _connection;

    /// <summary>
    /// Toujours false car la connexion est gérée par le client.
    /// </summary>
    public bool OwnsConnection => false;

    public ExternalConnexionBD(IDbConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        ConnexionID = Guid.NewGuid();
        _connection = connection;

        // S'assurer que la connexion est ouverte
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    /// <summary>
    /// Ne ferme PAS la connexion car elle est gérée extérieurement.
    /// </summary>
    public void Close()
    {
        // Ne rien faire - la connexion appartient au client
    }

    /// <summary>
    /// Ne ferme PAS la connexion car elle est gérée extérieurement.
    /// </summary>
    public void Dispose()
    {
        // Ne rien faire - la connexion appartient au client
    }
}
