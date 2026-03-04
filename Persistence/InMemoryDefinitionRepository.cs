using System.Collections.Concurrent;

namespace SimpleBPM.Persistence;

/// <summary>
/// Implémentation en mémoire de <see cref="IDefinitionRepository"/>.
/// Utilisée pour les tests et les scénarios sans base de données.
/// </summary>
public class InMemoryDefinitionRepository : IDefinitionRepository
{
    private readonly ConcurrentDictionary<(string Name, string Version), ProcessDefinition> _definitions = new();

    public Task SaveDefinitionAsync(ProcessDefinition definition)
    {
        _definitions[(definition.Name, definition.Version)] = definition;
        return Task.CompletedTask;
    }

    public Task<ProcessDefinition?> GetDefinitionAsync(string name, string version)
    {
        _definitions.TryGetValue((name, version), out var def);
        return Task.FromResult(def);
    }

    public Task<List<ProcessDefinition>> GetAllDefinitionsAsync()
    {
        return Task.FromResult(_definitions.Values.ToList());
    }

    public Task<List<string>> GetDefinitionVersionsAsync(string name)
    {
        var versions = _definitions.Keys
            .Where(k => k.Name == name)
            .Select(k => k.Version)
            .ToList();
        return Task.FromResult(versions);
    }

    public Task DeleteDefinitionAsync(string name, string version)
    {
        _definitions.TryRemove((name, version), out _);
        return Task.CompletedTask;
    }
}
