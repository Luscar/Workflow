namespace SimpleBPM.Monitor.Models;

public class TerminateRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ForceCompleteNodeRequest
{
    public string NodeId { get; set; } = string.Empty;
    public Dictionary<string, object>? OutputVariables { get; set; }
    public string? Reason { get; set; }
}

public class SendSignalRequest
{
    public string SignalName { get; set; } = string.Empty;
}

public class CreateInstanceRequest
{
    public string DefinitionName { get; set; } = string.Empty;
    public Dictionary<string, object>? Variables { get; set; }
}

public class SetVariablesRequest
{
    public Dictionary<string, object> Variables { get; set; } = new();
}

public class ActionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? InstanceId { get; set; }

    public static ActionResultDto Ok(string message, string? instanceId = null) =>
        new() { Success = true, Message = message, InstanceId = instanceId };

    public static ActionResultDto Fail(string message) =>
        new() { Success = false, Message = message };
}

public class AuditLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? User { get; set; }
}
