namespace SimpleBPM.Abstractions;

public interface IGestionTache
{
    Task CreerTacheAsync(long processId, long? aggregateId, string definitionName, string nodeName);
    Task FermerTacheAsync(long processId, long? aggregateId, string definitionName, string nodeName);
}
