using SimpleBPM.Monitor.Models;

namespace SimpleBPM.Monitor.Services;

public interface IMonitorService
{
    // Dashboard
    Task<DashboardDto> GetDashboardAsync();

    // Definitions
    List<ProcessDefinitionDto> GetAllDefinitions();
    ProcessDefinitionDto? GetDefinition(string name, string? version = null);

    // Instances
    Task<InstanceListResponse> GetInstancesAsync(
        int page = 1,
        int pageSize = 50,
        string? status = null,
        string? definitionName = null,
        string? searchTerm = null);
    Task<ProcessInstanceDto?> GetInstanceAsync(string processId);
    Task<List<ProcessInstanceDto>> GetChildInstancesAsync(string parentId);

    // Admin actions
    Task<ActionResultDto> CreateInstanceAsync(CreateInstanceRequest request);
    Task<ActionResultDto> TerminateInstanceAsync(string processId, TerminateRequest request);
    Task<ActionResultDto> ForceCompleteNodeAsync(string processId, ForceCompleteNodeRequest request);
    Task<ActionResultDto> SendSignalAsync(string processId, SendSignalRequest request);
    Task<ActionResultDto> SetVariablesAsync(string processId, SetVariablesRequest request);
    Task<ActionResultDto> RetryInstanceAsync(string processId);
    Task<ActionResultDto> DeleteInstanceAsync(string processId);

    // Audit
    List<AuditLogEntry> GetAuditLog(int count = 100);
}
