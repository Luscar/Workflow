namespace SimpleBPM.Blazor.Services;

/// <summary>
/// Environment parameters used to build a database connection for a given
/// Niveau / Phase / Cellule selection.
/// A new instance is registered into a child Autofac lifetime scope each time
/// the user applies an environment, so that IAccesDbPlus resolved from that
/// scope automatically uses the correct parameters.
/// </summary>
public interface IParamUtilisateur
{
    Niveau Niveau { get; }
    int Phase { get; }
    string Cellule { get; }
}

/// <summary>Immutable snapshot of environment parameters.</summary>
public sealed class ParamUtilisateur : IParamUtilisateur
{
    public Niveau Niveau { get; }
    public int Phase { get; }
    public string Cellule { get; }

    public ParamUtilisateur(Niveau niveau, int phase, string cellule)
    {
        Niveau = niveau;
        Phase = Math.Clamp(phase, 1, 9);
        Cellule = cellule.Trim().ToUpper();
    }
}
