using System.Collections.Concurrent;

namespace SimpleBPM.Persistence;

/// <summary>
/// Implémentation en mémoire de <see cref="IDefinitionRepository"/> — idéale pour les tests
/// et les scénarios sans persistance.
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
        _definitions.TryGetValue((name, version), out var definition);
        return Task.FromResult(definition);
    }

    public Task<List<ProcessDefinition>> GetDefinitionsByNameAsync(string name)
    {
        var results = _definitions.Values
            .Where(d => d.Name == name)
            .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<ProcessDefinition>> GetAllDefinitionsAsync()
    {
        var results = _definitions.Values
            .OrderBy(d => d.Name)
            .ThenByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
            .ToList();
        return Task.FromResult(results);
    }

    public Task DeleteDefinitionAsync(string name, string version)
    {
        _definitions.TryRemove((name, version), out _);
        return Task.CompletedTask;
    }
}
