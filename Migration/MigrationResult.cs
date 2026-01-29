namespace SimpleBPM.Migration;

public class MigrationResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string PreviousVersion { get; }
    public string NewVersion { get; }
    public string? PreviousNodeId { get; }
    public string? NewNodeId { get; }

    private MigrationResult(bool success, string previousVersion, string newVersion, string? previousNodeId, string? newNodeId, string? errorMessage)
    {
        Success = success;
        PreviousVersion = previousVersion;
        NewVersion = newVersion;
        PreviousNodeId = previousNodeId;
        NewNodeId = newNodeId;
        ErrorMessage = errorMessage;
    }

    internal static MigrationResult Succeeded(string previousVersion, string newVersion, string? previousNodeId, string? newNodeId)
        => new(true, previousVersion, newVersion, previousNodeId, newNodeId, null);

    internal static MigrationResult Failed(string previousVersion, string newVersion, string errorMessage)
        => new(false, previousVersion, newVersion, null, null, errorMessage);
}
