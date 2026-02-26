namespace SimpleBPM.Blazor.Services;

/// <summary>
/// Database accessor scoped to a specific environment (Niveau / Phase / Cellule).
/// Resolved from a child Autofac lifetime scope that has IParamUtilisateur registered,
/// so each environment change produces a fresh, correctly-wired instance.
/// </summary>
public interface IAccesDbPlus
{
    // Add your data-access methods here, e.g.:
    // Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
}

/// <summary>
/// Concrete implementation — replace the constructor body with your real
/// connection-string / Oracle schema logic using the injected IParamUtilisateur.
/// </summary>
public sealed class AccesDbPlus : IAccesDbPlus
{
    private readonly IParamUtilisateur _param;

    public AccesDbPlus(IParamUtilisateur param)
    {
        _param = param;
        // TODO: open / configure your DB connection from _param
        // e.g. ConnectionString = BuildConnectionString(_param.Niveau, _param.Phase, _param.Cellule);
    }
}
