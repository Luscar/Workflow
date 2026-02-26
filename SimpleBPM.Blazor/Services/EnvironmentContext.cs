namespace SimpleBPM.Blazor.Services;

public enum Niveau
{
    Unitaire,
    Acceptation,
    Production
}

/// <summary>
/// Scoped service holding the user's currently selected environment.
/// Drives which database connection is used to load instance data.
/// </summary>
public class EnvironmentContext
{
    public Niveau Niveau { get; private set; } = Niveau.Unitaire;
    public int Phase { get; private set; } = 1;
    public string Cellule { get; private set; } = "AA";

    public bool IsConfigured { get; private set; } = false;

    public event Action? OnChange;

    public void SetEnvironment(Niveau niveau, int phase, string cellule)
    {
        Niveau = niveau;
        Phase = Math.Clamp(phase, 1, 9);
        Cellule = cellule.Trim().ToUpper();
        IsConfigured = true;
        OnChange?.Invoke();
    }

    public string GetDescription() =>
        IsConfigured
            ? $"{Niveau} / Phase {Phase} / Cellule {Cellule}"
            : "Not configured";
}
