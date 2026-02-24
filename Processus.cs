namespace SimpleBPM;

public class Processus
{
    public long Id { get; set; }
    public long? AggregateId { get; set; }
    public string? DefinitionName { get; set; }
    public string? DefinitionVersion { get; set; }
    public ProcessStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? CurrentNodeName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    internal static Processus FromInstance(ProcessInstance instance) => new()
    {
        Id = instance.ProcessId,
        AggregateId = instance.AggregateId,
        DefinitionName = instance.DefinitionName,
        DefinitionVersion = instance.DefinitionVersion,
        Status = instance.Status,
        ErrorMessage = instance.ErrorMessage,
        Variables = new Dictionary<string, object>(instance.Variables),
        CurrentNodeName = instance.CurrentNodeName,
        StartedAt = instance.StartedAt,
        CompletedAt = instance.CompletedAt
    };
}
