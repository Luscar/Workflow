namespace SimpleBPM.Persistence;

/// <summary>
/// Référentiel pour la persistance des définitions de processus en banque de données.
/// Permet de sauvegarder, charger et gérer les versions des définitions.
/// </summary>
public interface IDefinitionRepository
{
    /// <summary>
    /// Sauvegarde une définition de processus. Si une définition avec le même nom et la même version
    /// existe déjà, elle est mise à jour. Sinon, une nouvelle entrée est créée.
    /// </summary>
    Task SaveDefinitionAsync(ProcessDefinition definition);

    /// <summary>
    /// Charge une définition de processus par son nom et sa version.
    /// Retourne null si la définition n'existe pas.
    /// </summary>
    Task<ProcessDefinition?> GetDefinitionAsync(string name, string version);

    /// <summary>
    /// Retourne toutes les définitions de processus enregistrées en banque.
    /// </summary>
    Task<List<ProcessDefinition>> GetAllDefinitionsAsync();

    /// <summary>
    /// Retourne la liste des versions disponibles pour une définition donnée.
    /// </summary>
    Task<List<string>> GetDefinitionVersionsAsync(string name);

    /// <summary>
    /// Supprime une version spécifique d'une définition de processus.
    /// </summary>
    Task DeleteDefinitionAsync(string name, string version);
}
