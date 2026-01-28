using System.Data;

namespace SimpleBPM.Persistence;

public interface IConnexionBDFactory
{
    IConnexionBD CreateConnexion();
}
