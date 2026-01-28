using System.Data;

namespace SimpleBPM.Persistence;

public interface IConnexionBD
{
    Guid ConnexionID { get; }
    IDbConnection Connexion { get; }
    void Close();
}
