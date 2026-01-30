namespace SimpleBPM.Abstractions;

public interface IGestionTache
{
    Task CreerTacheAsync(string processId, string? aggregateId, string definitionName, string nodeName);
    Task FermerTacheAsync(string processId, string? aggregateId, string definitionName, string nodeName);
}
