using System.Data;

namespace SimpleBPM.Persistence;

public interface IConnexionBD : IDisposable
{
    Guid ConnexionID { get; }
    IDbConnection Connexion { get; }

    /// <summary>
    /// Indique si cette instance est propriétaire de la connexion.
    /// Si true, la connexion sera fermée lors du Dispose/Close.
    /// Si false, la connexion est gérée extérieurement et ne sera pas fermée.
    /// </summary>
    bool OwnsConnection { get; }

    void Close();
}
