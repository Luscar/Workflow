namespace SimpleBPM.Persistence;

/// <summary>
/// Banque de définitions de processus permettant de sauvegarder, charger
/// et gérer les versions de <see cref="ProcessDefinition"/>.
/// </summary>
public interface IDefinitionRepository
{
    /// <summary>
    /// Sauvegarde ou met à jour une définition de processus dans la banque.
    /// </summary>
    Task SaveDefinitionAsync(ProcessDefinition definition);

    /// <summary>
    /// Retourne une définition de processus par nom et version exacte,
    /// ou <c>null</c> si introuvable.
    /// </summary>
    Task<ProcessDefinition?> GetDefinitionAsync(string name, string version);

    /// <summary>
    /// Retourne toutes les versions d'une définition de processus par nom,
    /// triées de la plus récente à la plus ancienne.
    /// </summary>
    Task<List<ProcessDefinition>> GetDefinitionsByNameAsync(string name);

    /// <summary>
    /// Retourne toutes les définitions de processus présentes dans la banque.
    /// </summary>
    Task<List<ProcessDefinition>> GetAllDefinitionsAsync();

    /// <summary>
    /// Supprime une version spécifique d'une définition de processus.
    /// </summary>
    Task DeleteDefinitionAsync(string name, string version);
}
