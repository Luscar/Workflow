using SimpleBPM.Monitor.Models;
using SimpleBPM.Persistence;

namespace SimpleBPM.Monitor.Services;

public class MonitorService : IMonitorService
{
    private readonly FlowEngine _engine;
    private readonly IFlowService _flowService;
    private readonly IProcessRepository _repository;
    private readonly IEnumerable<ProcessDefinition> _definitions;
    private readonly List<AuditLogEntry> _auditLog = new();
    private static readonly object _auditLock = new();

    public MonitorService(
        FlowEngine engine,
        IFlowService flowService,
        IProcessRepository repository,
        IEnumerable<ProcessDefinition> definitions)
    {
        _engine = engine;
        _flowService = flowService;
        _repository = repository;
        _definitions = definitions;
    }

    // --- Dashboard ---

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var allInstances = await _repository.SearchByVariableAsync(new Dictionary<string, object>());

        var dashboard = new DashboardDto
        {
            TotalInstances = allInstances.Count,
            RunningCount = allInstances.Count(i => i.Status == ProcessStatus.Running),
            WaitingInteractionCount = allInstances.Count(i => i.Status == ProcessStatus.WaitingInteraction),
            WaitingSignalCount = allInstances.Count(i => i.Status == ProcessStatus.WaitingSignal),
            WaitingDateCount = allInstances.Count(i => i.Status == ProcessStatus.WaitingDate),
            CompletedCount = allInstances.Count(i => i.Status == ProcessStatus.Completed),
            FailedCount = allInstances.Count(i => i.Status == ProcessStatus.Failed),
            DefinitionCount = _definitions.Count()
        };

        dashboard.StatusBreakdown = new List<StatusBreakdownItem>
        {
            new() { Status = "Running", Count = dashboard.RunningCount, Color = "#22c55e" },
            new() { Status = "Waiting Interaction", Count = dashboard.WaitingInteractionCount, Color = "#f59e0b" },
            new() { Status = "Waiting Signal", Count = dashboard.WaitingSignalCount, Color = "#8b5cf6" },
            new() { Status = "Waiting Date", Count = dashboard.WaitingDateCount, Color = "#06b6d4" },
            new() { Status = "Completed", Count = dashboard.CompletedCount, Color = "#6b7280" },
            new() { Status = "Failed", Count = dashboard.FailedCount, Color = "#ef4444" }
        };

        dashboard.DefinitionSummaries = _definitions.Select(d => new DefinitionSummaryDto
        {
            Name = d.Name,
            Version = d.Version,
            NodeCount = d.Nodes.Count,
            ActiveInstances = allInstances.Count(i =>
                i.DefinitionName == d.Name &&
                i.Status != ProcessStatus.Completed &&
                i.Status != ProcessStatus.Failed)
        }).ToList();

        dashboard.RecentFailed = allInstances
            .Where(i => i.Status == ProcessStatus.Failed)
            .OrderByDescending(i => i.LastExecutedAt ?? i.StartedAt)
            .Take(10)
            .Select(MapToDto)
            .ToList();

        dashboard.LongRunning = allInstances
            .Where(i => i.Status != ProcessStatus.Completed && i.Status != ProcessStatus.Failed)
            .OrderBy(i => i.StartedAt)
            .Take(10)
            .Select(MapToDto)
            .ToList();

        return dashboard;
    }

    // --- Definitions ---

    public List<ProcessDefinitionDto> GetAllDefinitions()
    {
        return _definitions.Select(MapDefinitionToDto).ToList();
    }

    public ProcessDefinitionDto? GetDefinition(string name, string? version = null)
    {
        var def = version != null
            ? _definitions.FirstOrDefault(d => d.Name == name && d.Version == version)
            : _definitions.Where(d => d.Name == name)
                .OrderByDescending(d => Version.TryParse(d.Version, out var v) ? v : new Version(0, 0))
                .FirstOrDefault();

        return def != null ? MapDefinitionToDto(def) : null;
    }

    // --- Instances ---

    public async Task<InstanceListResponse> GetInstancesAsync(
        int page = 1,
        int pageSize = 50,
        string? status = null,
        string? definitionName = null,
        string? searchTerm = null)
    {
        var allInstances = await _repository.SearchByVariableAsync(new Dictionary<string, object>());

        var filtered = allInstances.AsEnumerable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ProcessStatus>(status, true, out var statusEnum))
        {
            filtered = filtered.Where(i => i.Status == statusEnum);
        }

        if (!string.IsNullOrEmpty(definitionName))
        {
            filtered = filtered.Where(i =>
                i.DefinitionName != null &&
                i.DefinitionName.Contains(definitionName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            filtered = filtered.Where(i =>
                i.ProcessId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (i.AggregateId != null && i.AggregateId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (i.DefinitionName != null && i.DefinitionName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filtered
            .OrderByDescending(i => i.LastExecutedAt ?? i.StartedAt)
            .ToList();

        return new InstanceListResponse
        {
            TotalCount = filteredList.Count,
            Page = page,
            PageSize = pageSize,
            Items = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList()
        };
    }

    public async Task<ProcessInstanceDto?> GetInstanceAsync(string processId)
    {
        var instance = await _repository.GetProcessInstanceAsync(processId);
        if (instance == null) return null;

        var dto = MapToDto(instance);

        // Resolve current node name from definition
        if (instance.CurrentNodeId != null && instance.DefinitionName != null)
        {
            var def = _definitions.FirstOrDefault(d =>
                d.Name == instance.DefinitionName && d.Version == instance.DefinitionVersion);
            if (def != null)
            {
                var node = def.GetNode(instance.CurrentNodeId);
                dto.CurrentNodeName = node?.Name;
            }
        }

        // Get pending signals
        if (instance.Status == ProcessStatus.WaitingSignal &&
            instance.InternalState.TryGetValue("WaitingForSignal", out var signal))
        {
            dto.PendingSignals = new List<string> { signal?.ToString() ?? "" };
        }

        return dto;
    }

    public async Task<List<ProcessInstanceDto>> GetChildInstancesAsync(string parentId)
    {
        var children = await _flowService.ObtenirEnfants(parentId);
        return children.Select(c => new ProcessInstanceDto
        {
            Id = c.Id,
            AggregateId = c.AggregateId,
            DefinitionName = c.DefinitionName,
            DefinitionVersion = c.DefinitionVersion,
            Status = c.Status.ToString(),
            ErrorMessage = c.ErrorMessage,
            Variables = c.Variables,
            CurrentNodeId = c.CurrentNodeId,
            StartedAt = c.StartedAt,
            CompletedAt = c.CompletedAt
        }).ToList();
    }

    // --- Admin Actions ---

    public async Task<ActionResultDto> CreateInstanceAsync(CreateInstanceRequest request)
    {
        try
        {
            var processId = await _flowService.CreateProcessInstance(request.DefinitionName, request.Variables);
            AddAuditEntry("CreateInstance", processId, $"Created instance for definition '{request.DefinitionName}'");
            return ActionResultDto.Ok($"Instance created successfully", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to create instance: {ex.Message}");
        }
    }

    public async Task<ActionResultDto> TerminateInstanceAsync(string processId, TerminateRequest request)
    {
        try
        {
            var instance = await _repository.GetProcessInstanceAsync(processId);
            if (instance == null)
                return ActionResultDto.Fail($"Instance '{processId}' not found");

            if (instance.Status == ProcessStatus.Completed || instance.Status == ProcessStatus.Failed)
                return ActionResultDto.Fail($"Instance already in terminal state: {instance.Status}");

            instance.Status = ProcessStatus.Failed;
            instance.ErrorMessage = $"Terminated by admin: {request.Reason}";
            instance.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateProcessInstanceAsync(instance);

            AddAuditEntry("Terminate", processId, $"Reason: {request.Reason}");
            return ActionResultDto.Ok($"Instance '{processId}' terminated", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to terminate: {ex.Message}");
        }
    }

    public async Task<ActionResultDto> ForceCompleteNodeAsync(string processId, ForceCompleteNodeRequest request)
    {
        try
        {
            var instance = await _repository.GetProcessInstanceAsync(processId);
            if (instance == null)
                return ActionResultDto.Fail($"Instance '{processId}' not found");

            if (instance.Status == ProcessStatus.Completed || instance.Status == ProcessStatus.Failed)
                return ActionResultDto.Fail($"Instance already in terminal state: {instance.Status}");

            // Apply output variables if provided
            if (request.OutputVariables != null)
            {
                foreach (var kvp in request.OutputVariables)
                    instance.Variables[kvp.Key] = kvp.Value;
            }

            // Force continue past the current node
            instance.Status = ProcessStatus.Running;
            await _engine.ContinueAsync(instance);

            AddAuditEntry("ForceCompleteNode", processId,
                $"Forced node '{request.NodeId}' to complete. Reason: {request.Reason}");
            return ActionResultDto.Ok($"Node forced to complete, process continued", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to force complete: {ex.Message}");
        }
    }

    public async Task<ActionResultDto> SendSignalAsync(string processId, SendSignalRequest request)
    {
        try
        {
            var instance = await _repository.GetProcessInstanceAsync(processId);
            if (instance == null)
                return ActionResultDto.Fail($"Instance '{processId}' not found");

            if (instance.Status != ProcessStatus.WaitingSignal)
                return ActionResultDto.Fail($"Instance is not waiting for a signal (status: {instance.Status})");

            await _engine.SignalAsync(instance, request.SignalName);

            AddAuditEntry("SendSignal", processId, $"Signal sent: {request.SignalName}");
            return ActionResultDto.Ok($"Signal '{request.SignalName}' sent", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to send signal: {ex.Message}");
        }
    }

    public async Task<ActionResultDto> SetVariablesAsync(string processId, SetVariablesRequest request)
    {
        try
        {
            var instance = await _repository.GetProcessInstanceAsync(processId);
            if (instance == null)
                return ActionResultDto.Fail($"Instance '{processId}' not found");

            foreach (var kvp in request.Variables)
                instance.Variables[kvp.Key] = kvp.Value;

            await _repository.UpdateProcessInstanceAsync(instance);

            AddAuditEntry("SetVariables", processId,
                $"Variables updated: {string.Join(", ", request.Variables.Keys)}");
            return ActionResultDto.Ok("Variables updated", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to set variables: {ex.Message}");
        }
    }

    public async Task<ActionResultDto> RetryInstanceAsync(string processId)
    {
        try
        {
            var instance = await _repository.GetProcessInstanceAsync(processId);
            if (instance == null)
                return ActionResultDto.Fail($"Instance '{processId}' not found");

            if (instance.Status != ProcessStatus.Failed)
                return ActionResultDto.Fail($"Only failed instances can be retried (status: {instance.Status})");

            instance.Status = ProcessStatus.Running;
            instance.ErrorMessage = null;
            await _engine.ExecuteAsync(instance);

            AddAuditEntry("Retry", processId, "Retried failed instance");
            return ActionResultDto.Ok("Instance retried", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to retry: {ex.Message}");
        }
    }

    public async Task<ActionResultDto> DeleteInstanceAsync(string processId)
    {
        try
        {
            var instance = await _repository.GetProcessInstanceAsync(processId);
            if (instance == null)
                return ActionResultDto.Fail($"Instance '{processId}' not found");

            await _repository.DeleteProcessInstanceAsync(processId);

            AddAuditEntry("Delete", processId, "Instance deleted");
            return ActionResultDto.Ok("Instance deleted", processId);
        }
        catch (Exception ex)
        {
            return ActionResultDto.Fail($"Failed to delete: {ex.Message}");
        }
    }

    // --- Audit ---

    public List<AuditLogEntry> GetAuditLog(int count = 100)
    {
        lock (_auditLock)
        {
            return _auditLog
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    // --- Private Helpers ---

    private void AddAuditEntry(string action, string instanceId, string? details = null)
    {
        lock (_auditLock)
        {
            _auditLog.Add(new AuditLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                InstanceId = instanceId,
                Details = details
            });

            // Keep last 1000 entries
            if (_auditLog.Count > 1000)
                _auditLog.RemoveRange(0, _auditLog.Count - 1000);
        }
    }

    private static ProcessInstanceDto MapToDto(ProcessInstance instance)
    {
        return new ProcessInstanceDto
        {
            Id = instance.ProcessId,
            AggregateId = instance.AggregateId,
            DefinitionName = instance.DefinitionName,
            DefinitionVersion = instance.DefinitionVersion,
            Status = instance.Status.ToString(),
            ErrorMessage = instance.ErrorMessage,
            Variables = new Dictionary<string, object>(instance.Variables),
            CurrentNodeId = instance.CurrentNodeId,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt,
            LastExecutedAt = instance.LastExecutedAt,
            Duration = FormatDuration(instance.CurrentDuration),
            CompletedSteps = instance.CompletedStepsCount,
            FailedSteps = instance.FailedStepsCount,
            SubProcessIds = new Dictionary<string, string>(instance.SubProcessIds),
            ExecutionHistory = instance.ExecutionHistory.Select(h => new NodeExecutionHistoryDto
            {
                NodeId = h.NodeId,
                NodeName = h.NodeName,
                NodeType = h.NodeType.ToString(),
                StartedAt = h.StartedAt,
                CompletedAt = h.CompletedAt,
                Duration = FormatDuration(h.Duration),
                Success = h.Success,
                ErrorMessage = h.ErrorMessage,
                NextNodeId = h.NextNodeId
            }).ToList()
        };
    }

    private ProcessDefinitionDto MapDefinitionToDto(ProcessDefinition def)
    {
        var dto = new ProcessDefinitionDto
        {
            Id = def.Id,
            Name = def.Name,
            Version = def.Version,
            StartNodeId = def.StartNodeId,
            Nodes = def.Nodes.Values.Select(n => new ProcessNodeDto
            {
                Id = n.Id,
                Name = n.Name,
                Type = n.Type.ToString(),
                IsStartNode = n.Id == def.StartNodeId,
                NextNodeIds = new List<string>(n.NextNodeIds),
                Metadata = GetNodeMetadata(n)
            }).ToList()
        };

        // Build connections
        dto.Connections = new List<NodeConnectionDto>();
        foreach (var node in def.Nodes.Values)
        {
            if (node is DecisionNode decisionNode)
            {
                foreach (var route in decisionNode.Routes)
                {
                    dto.Connections.Add(new NodeConnectionDto
                    {
                        FromNodeId = node.Id,
                        ToNodeId = route.Value,
                        Label = route.Key
                    });
                }
            }
            else
            {
                foreach (var nextId in node.NextNodeIds)
                {
                    dto.Connections.Add(new NodeConnectionDto
                    {
                        FromNodeId = node.Id,
                        ToNodeId = nextId
                    });
                }
            }
        }

        return dto;
    }

    private static Dictionary<string, object>? GetNodeMetadata(ProcessNode node)
    {
        var meta = new Dictionary<string, object>();

        switch (node)
        {
            case Nodes.BusinessNode bn:
                meta["command"] = bn.CommandName;
                break;
            case DecisionNode dn:
                meta["decision"] = dn.DecisionName;
                meta["routes"] = dn.Routes;
                break;
            case Nodes.WaitForSignalNode sn:
                meta["signalName"] = sn.SignalName;
                break;
            case Nodes.SubProcessNode sp:
                meta["subProcessName"] = sp.SubProcessDefinition?.Name ?? "Unknown";
                meta["inputMapping"] = sp.InputMapping;
                meta["outputMapping"] = sp.OutputMapping;
                break;
        }

        return meta.Count > 0 ? meta : null;
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalSeconds >= 1)
            return $"{ts.Seconds}.{ts.Milliseconds:D3}s";
        return $"{ts.Milliseconds}ms";
    }
}
