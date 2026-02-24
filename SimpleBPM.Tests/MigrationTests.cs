using SimpleBPM;
using SimpleBPM.Migration;
using SimpleBPM.Nodes;

namespace SimpleBPM.Tests;

public class VariableTransformTests
{
    [Fact]
    public void Set_CreatesSetTransform()
    {
        var transform = VariableTransform.Set("key", "value");

        Assert.Equal(VariableTransformType.Set, transform.Type);
        Assert.Equal("key", transform.Name);
        Assert.Equal("value", transform.Value);
        Assert.Null(transform.NewName);
    }

    [Fact]
    public void Rename_CreatesRenameTransform()
    {
        var transform = VariableTransform.Rename("oldName", "newName");

        Assert.Equal(VariableTransformType.Rename, transform.Type);
        Assert.Equal("oldName", transform.Name);
        Assert.Equal("newName", transform.NewName);
        Assert.Null(transform.Value);
    }

    [Fact]
    public void Remove_CreatesRemoveTransform()
    {
        var transform = VariableTransform.Remove("key");

        Assert.Equal(VariableTransformType.Remove, transform.Type);
        Assert.Equal("key", transform.Name);
        Assert.Null(transform.NewName);
        Assert.Null(transform.Value);
    }

    [Fact]
    public void Apply_Set_AddsNewVariable()
    {
        var variables = new Dictionary<string, object>();
        var transform = VariableTransform.Set("newKey", 42);

        transform.Apply(variables);

        Assert.Equal(42, variables["newKey"]);
    }

    [Fact]
    public void Apply_Set_OverwritesExistingVariable()
    {
        var variables = new Dictionary<string, object> { ["key"] = "old" };
        var transform = VariableTransform.Set("key", "new");

        transform.Apply(variables);

        Assert.Equal("new", variables["key"]);
    }

    [Fact]
    public void Apply_Rename_MovesVariable()
    {
        var variables = new Dictionary<string, object> { ["oldName"] = "value123" };
        var transform = VariableTransform.Rename("oldName", "newName");

        transform.Apply(variables);

        Assert.False(variables.ContainsKey("oldName"));
        Assert.Equal("value123", variables["newName"]);
    }

    [Fact]
    public void Apply_Rename_NonExistentVariable_DoesNothing()
    {
        var variables = new Dictionary<string, object> { ["other"] = "value" };
        var transform = VariableTransform.Rename("missing", "newName");

        transform.Apply(variables);

        Assert.False(variables.ContainsKey("newName"));
        Assert.Single(variables);
    }

    [Fact]
    public void Apply_Remove_DeletesVariable()
    {
        var variables = new Dictionary<string, object> { ["key"] = "value", ["other"] = "keep" };
        var transform = VariableTransform.Remove("key");

        transform.Apply(variables);

        Assert.False(variables.ContainsKey("key"));
        Assert.Single(variables);
        Assert.Equal("keep", variables["other"]);
    }

    [Fact]
    public void Apply_Remove_NonExistentVariable_DoesNothing()
    {
        var variables = new Dictionary<string, object> { ["other"] = "value" };
        var transform = VariableTransform.Remove("missing");

        transform.Apply(variables);

        Assert.Single(variables);
    }
}

public class ProcessMigrationTests
{
    [Fact]
    public void Constructor_SetsVersions()
    {
        var migration = new ProcessMigration("1.0", "2.0");

        Assert.Equal("1.0", migration.FromVersion);
        Assert.Equal("2.0", migration.ToVersion);
        Assert.Empty(migration.NodeMappings);
        Assert.Empty(migration.VariableTransforms);
    }

    [Fact]
    public void MapNode_AddsMapping()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("OldNode", "NewNode");

        Assert.Single(migration.NodeMappings);
        Assert.Equal("NewNode", migration.NodeMappings["OldNode"]);
    }

    [Fact]
    public void MapNode_FluentChaining()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("A", "B")
            .MapNode("C", "D");

        Assert.Equal(2, migration.NodeMappings.Count);
    }

    [Fact]
    public void MapNode_OverwritesPreviousMapping()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Node", "First")
            .MapNode("Node", "Second");

        Assert.Single(migration.NodeMappings);
        Assert.Equal("Second", migration.NodeMappings["Node"]);
    }

    [Fact]
    public void SetVariable_AddsSetTransform()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .SetVariable("status", "active");

        Assert.Single(migration.VariableTransforms);
        Assert.Equal(VariableTransformType.Set, migration.VariableTransforms[0].Type);
        Assert.Equal("status", migration.VariableTransforms[0].Name);
        Assert.Equal("active", migration.VariableTransforms[0].Value);
    }

    [Fact]
    public void RenameVariable_AddsRenameTransform()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .RenameVariable("oldVar", "newVar");

        Assert.Single(migration.VariableTransforms);
        Assert.Equal(VariableTransformType.Rename, migration.VariableTransforms[0].Type);
    }

    [Fact]
    public void RemoveVariable_AddsRemoveTransform()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .RemoveVariable("obsolete");

        Assert.Single(migration.VariableTransforms);
        Assert.Equal(VariableTransformType.Remove, migration.VariableTransforms[0].Type);
    }

    [Fact]
    public void FluentChaining_AllTransformTypes()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("A", "B")
            .SetVariable("new", "value")
            .RenameVariable("old", "renamed")
            .RemoveVariable("gone");

        Assert.Single(migration.NodeMappings);
        Assert.Equal(3, migration.VariableTransforms.Count);
    }

    [Fact]
    public void ResolveNodeName_MappedNode_ReturnsMappedName()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("OldReview", "NewReview");

        var resolved = migration.ResolveNodeName("OldReview");

        Assert.Equal("NewReview", resolved);
    }

    [Fact]
    public void ResolveNodeName_UnmappedNode_ReturnsOriginalName()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("OldReview", "NewReview");

        var resolved = migration.ResolveNodeName("Unchanged");

        Assert.Equal("Unchanged", resolved);
    }

    [Fact]
    public void ApplyVariableTransforms_AppliesAllTransformsInOrder()
    {
        var migration = new ProcessMigration("1.0", "2.0")
            .SetVariable("newVar", "hello")
            .RenameVariable("oldName", "renamedVar")
            .RemoveVariable("deprecated");

        var variables = new Dictionary<string, object>
        {
            ["oldName"] = "existingValue",
            ["deprecated"] = "toRemove",
            ["untouched"] = "stays"
        };

        migration.ApplyVariableTransforms(variables);

        Assert.Equal("hello", variables["newVar"]);
        Assert.Equal("existingValue", variables["renamedVar"]);
        Assert.False(variables.ContainsKey("oldName"));
        Assert.False(variables.ContainsKey("deprecated"));
        Assert.Equal("stays", variables["untouched"]);
    }
}

public class MigrationResultTests
{
    [Fact]
    public void Succeeded_SetsAllProperties()
    {
        var result = MigrationResult.Succeeded("1.0", "2.0", "NodeA", "NodeB");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("1.0", result.PreviousVersion);
        Assert.Equal("2.0", result.NewVersion);
        Assert.Equal("NodeA", result.PreviousNodeId);
        Assert.Equal("NodeB", result.NewNodeId);
    }

    [Fact]
    public void Failed_SetsAllProperties()
    {
        var result = MigrationResult.Failed("1.0", "2.0", "Something went wrong");

        Assert.False(result.Success);
        Assert.Equal("Something went wrong", result.ErrorMessage);
        Assert.Equal("1.0", result.PreviousVersion);
        Assert.Equal("2.0", result.NewVersion);
        Assert.Null(result.PreviousNodeId);
        Assert.Null(result.NewNodeId);
    }
}

public class ProcessMigrationRunnerTests
{
    private static ProcessDefinition CreateSourceDefinition()
    {
        var def = new ProcessDefinition("TestProcess", "1.0");
        var interactive = new InteractiveNode { Name = "Review", DisplayName = "Review Step" };
        interactive.NextNodeIds.Add("Approve");
        var approve = new BusinessNode("ApproveCmd") { Name = "Approve", DisplayName = "Approve Step" };
        def.AddNode(interactive);
        def.AddNode(approve);
        return def;
    }

    private static ProcessDefinition CreateTargetDefinition()
    {
        var def = new ProcessDefinition("TestProcess", "2.0");
        var interactive = new InteractiveNode { Name = "NewReview", DisplayName = "New Review Step" };
        interactive.NextNodeIds.Add("Validate");
        var validate = new BusinessNode("ValidateCmd") { Name = "Validate", DisplayName = "Validate Step" };
        def.AddNode(interactive);
        def.AddNode(validate);
        return def;
    }

    [Theory]
    [InlineData(ProcessStatus.WaitingInteraction)]
    [InlineData(ProcessStatus.WaitingSignal)]
    [InlineData(ProcessStatus.WaitingDate)]
    public void Migrate_MigratableStatuses_Succeeds(ProcessStatus status)
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "NewReview");

        var instance = new ProcessInstance(1)
        {
            DefinitionName = "TestProcess",
            DefinitionVersion = "1.0",
            Status = status,
            CurrentNodeName = "Review"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.True(result.Success);
        Assert.Equal("1.0", result.PreviousVersion);
        Assert.Equal("2.0", result.NewVersion);
        Assert.Equal("Review", result.PreviousNodeId);
        Assert.Equal("NewReview", result.NewNodeId);
        Assert.Equal("NewReview", instance.CurrentNodeName);
        Assert.Equal("2.0", instance.DefinitionVersion);
    }

    [Theory]
    [InlineData(ProcessStatus.Running)]
    [InlineData(ProcessStatus.Completed)]
    [InlineData(ProcessStatus.Failed)]
    public void Migrate_NonMigratableStatuses_Fails(ProcessStatus status)
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0");

        var instance = new ProcessInstance(1)
        {
            Status = status,
            CurrentNodeName = "Review"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.False(result.Success);
        Assert.Contains(status.ToString(), result.ErrorMessage);
    }

    [Fact]
    public void Migrate_NullCurrentNodeName_Fails()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0");

        var instance = new ProcessInstance(1)
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = null
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.False(result.Success);
        Assert.Contains("current node", result.ErrorMessage);
    }

    [Fact]
    public void Migrate_EmptyCurrentNodeName_Fails()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0");

        var instance = new ProcessInstance(1)
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = ""
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.False(result.Success);
        Assert.Contains("current node", result.ErrorMessage);
    }

    [Fact]
    public void Migrate_SourceNodeNotFound_Fails()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0");

        var instance = new ProcessInstance(1)
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "NonExistentNode"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.False(result.Success);
        Assert.Contains("NonExistentNode", result.ErrorMessage);
        Assert.Contains("source definition", result.ErrorMessage);
    }

    [Fact]
    public void Migrate_TargetNodeNotFound_Fails()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "DoesNotExist");

        var instance = new ProcessInstance(1)
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "Review"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.False(result.Success);
        Assert.Contains("DoesNotExist", result.ErrorMessage);
        Assert.Contains("target definition", result.ErrorMessage);
    }

    [Fact]
    public void Migrate_UnmappedNode_UsesIdentityMapping()
    {
        var source = new ProcessDefinition("Process", "1.0");
        source.AddNode(new InteractiveNode { Name = "SharedNode", DisplayName = "Shared" });

        var target = new ProcessDefinition("Process", "2.0");
        target.AddNode(new InteractiveNode { Name = "SharedNode", DisplayName = "Shared Updated" });

        var migration = new ProcessMigration("1.0", "2.0");

        var instance = new ProcessInstance(1)
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "SharedNode"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.True(result.Success);
        Assert.Equal("SharedNode", result.NewNodeId);
    }

    [Fact]
    public void Migrate_AppliesVariableTransforms()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "NewReview")
            .SetVariable("migrated", true)
            .RenameVariable("oldStatus", "newStatus")
            .RemoveVariable("deprecated");

        var instance = new ProcessInstance(1)
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "Review"
        };
        instance.Variables["oldStatus"] = "pending";
        instance.Variables["deprecated"] = "removeMe";
        instance.Variables["untouched"] = "keepMe";

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.True(result.Success);
        Assert.Equal(true, instance.Variables["migrated"]);
        Assert.Equal("pending", instance.Variables["newStatus"]);
        Assert.False(instance.Variables.ContainsKey("oldStatus"));
        Assert.False(instance.Variables.ContainsKey("deprecated"));
        Assert.Equal("keepMe", instance.Variables["untouched"]);
    }

    [Fact]
    public void Migrate_UpdatesInstanceVersionAndNode()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "NewReview");

        var instance = new ProcessInstance(1)
        {
            DefinitionVersion = "1.0",
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "Review"
        };

        ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.Equal("2.0", instance.DefinitionVersion);
        Assert.Equal("NewReview", instance.CurrentNodeName);
    }

    [Fact]
    public void Migrate_UsesInstanceVersionWhenAvailable()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "NewReview");

        var instance = new ProcessInstance(1)
        {
            DefinitionVersion = "1.0",
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "Review"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.Equal("1.0", result.PreviousVersion);
    }

    [Fact]
    public void Migrate_FallsBackToSourceDefinitionVersion()
    {
        var source = CreateSourceDefinition();
        var target = CreateTargetDefinition();
        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "NewReview");

        var instance = new ProcessInstance(1)
        {
            DefinitionVersion = null,
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeName = "Review"
        };

        var result = ProcessMigrationRunner.Migrate(instance, source, target, migration);

        Assert.Equal("1.0", result.PreviousVersion);
    }
}

public class ProcessMigrationLoaderTests
{
    [Fact]
    public void FromJson_MinimalMigration_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0"
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Equal("1.0", migration.FromVersion);
        Assert.Equal("2.0", migration.ToVersion);
        Assert.Empty(migration.NodeMappings);
        Assert.Empty(migration.VariableTransforms);
    }

    [Fact]
    public void FromJson_WithNodeMappings_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "nodeMappings": {
                "OldReview": "NewReview",
                "OldApprove": "NewApprove"
            }
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(2, migration.NodeMappings.Count);
        Assert.Equal("NewReview", migration.NodeMappings["OldReview"]);
        Assert.Equal("NewApprove", migration.NodeMappings["OldApprove"]);
    }

    [Fact]
    public void FromJson_WithSetTransform_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "variableTransforms": [
                { "type": "set", "name": "status", "value": "migrated" }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Single(migration.VariableTransforms);
        Assert.Equal(VariableTransformType.Set, migration.VariableTransforms[0].Type);
        Assert.Equal("status", migration.VariableTransforms[0].Name);
        Assert.Equal("migrated", migration.VariableTransforms[0].Value);
    }

    [Fact]
    public void FromJson_WithNumericValue_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "variableTransforms": [
                { "type": "set", "name": "count", "value": 42 }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(42L, migration.VariableTransforms[0].Value);
    }

    [Fact]
    public void FromJson_WithBooleanValue_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "variableTransforms": [
                { "type": "set", "name": "active", "value": true }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(true, migration.VariableTransforms[0].Value);
    }

    [Fact]
    public void FromJson_WithRenameTransform_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "variableTransforms": [
                { "type": "rename", "name": "oldVar", "newName": "newVar" }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Single(migration.VariableTransforms);
        Assert.Equal(VariableTransformType.Rename, migration.VariableTransforms[0].Type);
        Assert.Equal("oldVar", migration.VariableTransforms[0].Name);
        Assert.Equal("newVar", migration.VariableTransforms[0].NewName);
    }

    [Fact]
    public void FromJson_WithRemoveTransform_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "variableTransforms": [
                { "type": "remove", "name": "deprecated" }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Single(migration.VariableTransforms);
        Assert.Equal(VariableTransformType.Remove, migration.VariableTransforms[0].Type);
        Assert.Equal("deprecated", migration.VariableTransforms[0].Name);
    }

    [Fact]
    public void FromJson_CaseInsensitiveType_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "variableTransforms": [
                { "type": "SET", "name": "key", "value": "val" }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(VariableTransformType.Set, migration.VariableTransforms[0].Type);
    }

    [Fact]
    public void FromJson_CompleteExample_ParsesCorrectly()
    {
        var json = """
        {
            "fromVersion": "1.0",
            "toVersion": "2.0",
            "nodeMappings": {
                "Review": "NewReview"
            },
            "variableTransforms": [
                { "type": "set", "name": "version", "value": "2.0" },
                { "type": "rename", "name": "oldVar", "newName": "newVar" },
                { "type": "remove", "name": "deprecated" }
            ]
        }
        """;

        var migration = ProcessMigrationLoader.FromJson(json);

        Assert.Equal("1.0", migration.FromVersion);
        Assert.Equal("2.0", migration.ToVersion);
        Assert.Single(migration.NodeMappings);
        Assert.Equal(3, migration.VariableTransforms.Count);
    }

    [Fact]
    public void FromJson_InvalidJson_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => ProcessMigrationLoader.FromJson("not valid json"));
    }

    [Fact]
    public void ToJson_MinimalMigration_SerializesCorrectly()
    {
        var migration = new ProcessMigration("1.0", "2.0");

        var json = ProcessMigrationLoader.ToJson(migration);
        var parsed = ProcessMigrationLoader.FromJson(json);

        Assert.Equal("1.0", parsed.FromVersion);
        Assert.Equal("2.0", parsed.ToVersion);
        Assert.Empty(parsed.NodeMappings);
        Assert.Empty(parsed.VariableTransforms);
    }

    [Fact]
    public void ToJson_RoundTrip_WithAllTransformTypes()
    {
        var original = new ProcessMigration("1.0", "2.0")
            .MapNode("OldNode", "NewNode")
            .SetVariable("key", "value")
            .RenameVariable("from", "to")
            .RemoveVariable("gone");

        var json = ProcessMigrationLoader.ToJson(original);
        var parsed = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(original.FromVersion, parsed.FromVersion);
        Assert.Equal(original.ToVersion, parsed.ToVersion);
        Assert.Equal(original.NodeMappings.Count, parsed.NodeMappings.Count);
        Assert.Equal("NewNode", parsed.NodeMappings["OldNode"]);
        Assert.Equal(original.VariableTransforms.Count, parsed.VariableTransforms.Count);

        Assert.Equal(VariableTransformType.Set, parsed.VariableTransforms[0].Type);
        Assert.Equal("key", parsed.VariableTransforms[0].Name);
        Assert.Equal("value", parsed.VariableTransforms[0].Value);

        Assert.Equal(VariableTransformType.Rename, parsed.VariableTransforms[1].Type);
        Assert.Equal("from", parsed.VariableTransforms[1].Name);
        Assert.Equal("to", parsed.VariableTransforms[1].NewName);

        Assert.Equal(VariableTransformType.Remove, parsed.VariableTransforms[2].Type);
        Assert.Equal("gone", parsed.VariableTransforms[2].Name);
    }

    [Fact]
    public void ToJson_RoundTrip_WithNumericValue()
    {
        var original = new ProcessMigration("1.0", "2.0")
            .SetVariable("count", 99);

        var json = ProcessMigrationLoader.ToJson(original);
        var parsed = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(99L, parsed.VariableTransforms[0].Value);
    }

    [Fact]
    public void ToJson_RoundTrip_WithBooleanValue()
    {
        var original = new ProcessMigration("1.0", "2.0")
            .SetVariable("flag", false);

        var json = ProcessMigrationLoader.ToJson(original);
        var parsed = ProcessMigrationLoader.FromJson(json);

        Assert.Equal(false, parsed.VariableTransforms[0].Value);
    }
}
