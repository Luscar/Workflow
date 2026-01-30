namespace SimpleBPM;

public class Processus
{
    public string Id { get; set; } = string.Empty;
    public string? AggregateId { get; set; }
    public string? DefinitionName { get; set; }
    public string? DefinitionVersion { get; set; }
    public ProcessStatus Status { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? CurrentNodeId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ParentProcessId { get; set; }
    public string? ParentNodeId { get; set; }

    internal static Processus FromInstance(ProcessInstance instance) => new()
    {
        Id = instance.ProcessId,
        AggregateId = instance.AggregateId,
        DefinitionName = instance.DefinitionName,
        DefinitionVersion = instance.DefinitionVersion,
        Status = instance.Status,
        Variables = instance.Variables,
        CurrentNodeId = instance.CurrentNodeId,
        StartedAt = instance.StartedAt,
        CompletedAt = instance.CompletedAt,
        ParentProcessId = instance.ParentProcessId,
        ParentNodeId = instance.ParentNodeId
    };
}
