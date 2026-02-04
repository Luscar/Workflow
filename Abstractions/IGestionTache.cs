namespace SimpleBPM.Abstractions;

public interface IGestionTache
{
    Task CreerTacheAsync(long processId, string? aggregateId, string definitionName, string nodeName);
    Task FermerTacheAsync(long processId, string? aggregateId, string definitionName, string nodeName);
}
