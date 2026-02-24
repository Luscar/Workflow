namespace SimpleBPM.Migration;

public static class ProcessMigrationRunner
{
    private static readonly HashSet<ProcessStatus> MigratableStatuses = new()
    {
        ProcessStatus.WaitingInteraction,
        ProcessStatus.WaitingSignal,
        ProcessStatus.WaitingDate
    };

    public static MigrationResult Migrate(
        ProcessInstance instance,
        ProcessDefinition sourceDefinition,
        ProcessDefinition targetDefinition,
        ProcessMigration migration)
    {
        var previousVersion = instance.DefinitionVersion ?? sourceDefinition.Version;
        var newVersion = targetDefinition.Version;

        if (!MigratableStatuses.Contains(instance.Status))
        {
            return MigrationResult.Failed(previousVersion, newVersion,
                $"Cannot migrate instance in status '{instance.Status}'. Only waiting/interactive instances can be migrated.");
        }

        if (string.IsNullOrEmpty(instance.CurrentNodeName))
        {
            return MigrationResult.Failed(previousVersion, newVersion,
                "Cannot migrate instance without a current node.");
        }

        // Resolve current node name from source definition
        var sourceNode = sourceDefinition.GetNode(instance.CurrentNodeName);
        if (sourceNode == null)
        {
            return MigrationResult.Failed(previousVersion, newVersion,
                $"Current node '{instance.CurrentNodeName}' not found in source definition.");
        }

        // Map node name (identity if not explicitly mapped)
        var targetNodeName = migration.ResolveNodeName(sourceNode.Name);

        // Find target node by name
        var targetNode = targetDefinition.GetNode(targetNodeName);
        if (targetNode == null)
        {
            return MigrationResult.Failed(previousVersion, newVersion,
                $"Target node '{targetNodeName}' not found in target definition.");
        }

        var previousNodeId = instance.CurrentNodeName;

        // Apply variable transforms
        migration.ApplyVariableTransforms(instance.Variables);

        // Update instance
        instance.CurrentNodeName = targetNode.Name;
        instance.DefinitionVersion = targetDefinition.Version;

        return MigrationResult.Succeeded(previousVersion, newVersion, previousNodeId, targetNode.Name);
    }
}
