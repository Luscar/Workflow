using SimpleBPM.Definition;
using SimpleBPM.Migration;

namespace SimpleBPM.Tests;

public class MigrationTests
{
    [Fact]
    public void Migrate_WaitingProcess_Succeeds()
    {
        var v1 = ProcessBuilder.Create("Process", "1.0")
            .Business("Start")
            .Interactive("Review", "Review")
            .Business("End")
            .Build();

        var v2 = ProcessBuilder.Create("Process", "2.0")
            .Business("Start")
            .Interactive("DetailedReview", "DetailedReview")
            .Business("End")
            .Build();

        var instance = new ProcessInstance("p1")
        {
            DefinitionName = "Process",
            DefinitionVersion = "1.0",
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("Review")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0")
            .MapNode("Review", "DetailedReview");

        var result = ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.True(result.Success);
        Assert.Equal("1.0", result.PreviousVersion);
        Assert.Equal("2.0", result.NewVersion);
        Assert.Equal(v2.GetNodeByName("DetailedReview")!.Id, instance.CurrentNodeId);
        Assert.Equal("2.0", instance.DefinitionVersion);
    }

    [Fact]
    public void Migrate_CompletedProcess_Fails()
    {
        var v1 = ProcessBuilder.Create("Process", "1.0")
            .Business("Step")
            .Build();
        var v2 = ProcessBuilder.Create("Process", "2.0")
            .Business("Step")
            .Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.Completed,
            CurrentNodeId = v1.GetNodeByName("Step")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0");
        var result = ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.False(result.Success);
        Assert.Contains("Cannot migrate", result.ErrorMessage);
    }

    [Fact]
    public void Migrate_RunningProcess_Fails()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Business("S").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Business("S").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.Running,
            CurrentNodeId = v1.GetNodeByName("S")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0");
        var result = ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.False(result.Success);
    }

    [Fact]
    public void Migrate_WaitingSignal_Succeeds()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").WaitForSignal("Sig").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").WaitForSignal("Sig").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingSignal,
            CurrentNodeId = v1.GetNodeByName("Sig")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0");
        var result = ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.True(result.Success);
    }

    [Fact]
    public void Migrate_MissingTargetNode_Fails()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Interactive("OldNode").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Interactive("NewNode").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("OldNode")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0");
        var result = ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.False(result.Success);
        Assert.Contains("not found in target", result.ErrorMessage);
    }

    [Fact]
    public void Migrate_SameNameNode_MapsAutomatically()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Interactive("SharedName").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Interactive("SharedName").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("SharedName")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0");
        var result = ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.True(result.Success);
        Assert.Equal(v2.GetNodeByName("SharedName")!.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void Migrate_VariableTransform_Set()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Interactive("N").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Interactive("N").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("N")!.Id
        };

        var migration = new ProcessMigration("1.0", "2.0")
            .SetVariable("MigratedFlag", true);

        ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.True((bool)instance.Variables["MigratedFlag"]);
    }

    [Fact]
    public void Migrate_VariableTransform_Rename()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Interactive("N").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Interactive("N").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("N")!.Id
        };
        instance.Variables["OldName"] = "value";

        var migration = new ProcessMigration("1.0", "2.0")
            .RenameVariable("OldName", "NewName");

        ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.False(instance.Variables.ContainsKey("OldName"));
        Assert.Equal("value", instance.Variables["NewName"]);
    }

    [Fact]
    public void Migrate_VariableTransform_Remove()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Interactive("N").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Interactive("N").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("N")!.Id
        };
        instance.Variables["Obsolete"] = "remove me";

        var migration = new ProcessMigration("1.0", "2.0")
            .RemoveVariable("Obsolete");

        ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.False(instance.Variables.ContainsKey("Obsolete"));
    }

    [Fact]
    public void Migrate_MultipleVariableTransforms_AppliedInOrder()
    {
        var v1 = ProcessBuilder.Create("P", "1.0").Interactive("N").Build();
        var v2 = ProcessBuilder.Create("P", "2.0").Interactive("N").Build();

        var instance = new ProcessInstance("p1")
        {
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = v1.GetNodeByName("N")!.Id
        };
        instance.Variables["Status"] = "old";

        var migration = new ProcessMigration("1.0", "2.0")
            .RenameVariable("Status", "ReviewStatus")
            .SetVariable("MigratedFromV1", true)
            .RemoveVariable("Unused");

        ProcessMigrationRunner.Migrate(instance, v1, v2, migration);

        Assert.Equal("old", instance.Variables["ReviewStatus"]);
        Assert.True((bool)instance.Variables["MigratedFromV1"]);
        Assert.False(instance.Variables.ContainsKey("Status"));
    }
}
