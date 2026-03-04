using SimpleBPM;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;

namespace SimpleBPM.Tests;

public class DefinitionRepositoryTests
{
    private static ProcessDefinition CreateTestDefinition(string name = "TestProcess", string version = "1.0")
    {
        var def = new ProcessDefinition(name, version);
        var node1 = new BusinessNode("Step1") { Name = "Step1", DisplayName = "Étape 1" };
        var node2 = new EndNode { Name = "End", DisplayName = "Fin" };
        node1.NextNodeIds.Add("End");
        def.AddNode(node1);
        def.AddNode(node2);
        def.StartNodeId = "Step1";
        return def;
    }

    [Fact]
    public async Task SaveAndGet_RoundTrip()
    {
        var repo = new InMemoryDefinitionRepository();
        var def = CreateTestDefinition();

        await repo.SaveDefinitionAsync(def);
        var loaded = await repo.GetDefinitionAsync("TestProcess", "1.0");

        Assert.NotNull(loaded);
        Assert.Equal("TestProcess", loaded.Name);
        Assert.Equal("1.0", loaded.Version);
        Assert.Equal("Step1", loaded.StartNodeId);
        Assert.Equal(2, loaded.Nodes.Count);
    }

    [Fact]
    public async Task GetDefinition_NotFound_ReturnsNull()
    {
        var repo = new InMemoryDefinitionRepository();

        var result = await repo.GetDefinitionAsync("Missing", "1.0");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveDefinition_OverwritesExisting()
    {
        var repo = new InMemoryDefinitionRepository();
        var def1 = CreateTestDefinition("Process", "1.0");
        await repo.SaveDefinitionAsync(def1);

        var def2 = new ProcessDefinition("Process", "1.0");
        var node = new EndNode { Name = "OnlyEnd", DisplayName = "Fin" };
        def2.AddNode(node);
        await repo.SaveDefinitionAsync(def2);

        var loaded = await repo.GetDefinitionAsync("Process", "1.0");
        Assert.NotNull(loaded);
        Assert.Single(loaded.Nodes);
    }

    [Fact]
    public async Task GetAllDefinitions_ReturnsAll()
    {
        var repo = new InMemoryDefinitionRepository();
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process1", "1.0"));
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process1", "2.0"));
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process2", "1.0"));

        var all = await repo.GetAllDefinitionsAsync();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetDefinitionVersions_ReturnsVersionsForName()
    {
        var repo = new InMemoryDefinitionRepository();
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process", "1.0"));
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process", "2.0"));
        await repo.SaveDefinitionAsync(CreateTestDefinition("Other", "1.0"));

        var versions = await repo.GetDefinitionVersionsAsync("Process");

        Assert.Equal(2, versions.Count);
        Assert.Contains("1.0", versions);
        Assert.Contains("2.0", versions);
    }

    [Fact]
    public async Task DeleteDefinition_RemovesIt()
    {
        var repo = new InMemoryDefinitionRepository();
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process", "1.0"));
        await repo.SaveDefinitionAsync(CreateTestDefinition("Process", "2.0"));

        await repo.DeleteDefinitionAsync("Process", "1.0");

        var v1 = await repo.GetDefinitionAsync("Process", "1.0");
        var v2 = await repo.GetDefinitionAsync("Process", "2.0");

        Assert.Null(v1);
        Assert.NotNull(v2);
    }

    [Fact]
    public async Task FlowEngine_LoadsDefinitionFromRepository()
    {
        var repo = new InMemoryDefinitionRepository();
        var def = CreateTestDefinition("StoredProcess", "1.0");
        await repo.SaveDefinitionAsync(def);

        // Engine créé sans définition en mémoire, mais avec le repository
        var engine = new FlowEngine(
            definitions: Array.Empty<ProcessDefinition>(),
            definitionRepository: repo);

        var instance = new ProcessInstance(1) { DefinitionName = "StoredProcess" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal("StoredProcess", result.DefinitionName);
        Assert.Equal("1.0", result.DefinitionVersion);
    }

    [Fact]
    public async Task FlowEngine_InMemoryDefinitionTakesPrecedenceOverRepository()
    {
        var repo = new InMemoryDefinitionRepository();

        // Version 1.0 dans le repo, version 2.0 en mémoire
        var defRepo = CreateTestDefinition("Process", "1.0");
        await repo.SaveDefinitionAsync(defRepo);

        var defMemory = CreateTestDefinition("Process", "2.0");

        var engine = new FlowEngine(
            definitions: new[] { defMemory },
            definitionRepository: repo);

        var instance = new ProcessInstance(1)
        {
            DefinitionName = "Process",
            DefinitionVersion = "2.0"
        };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
        Assert.Equal("2.0", result.DefinitionVersion);
    }

    [Fact]
    public async Task FlowService_SaveDefinition_PersistsToRepository()
    {
        var repo = new InMemoryDefinitionRepository();
        var processRepo = new InMemoryProcessRepository();
        var def = CreateTestDefinition("MyProcess", "1.0");

        var service = new FlowService(
            definitions: Array.Empty<ProcessDefinition>(),
            repository: processRepo,
            handlers: Array.Empty<SimpleBPM.Handlers.INodeHandler>(),
            definitionRepository: repo);

        await service.SaveDefinitionAsync(def);

        var loaded = await repo.GetDefinitionAsync("MyProcess", "1.0");
        Assert.NotNull(loaded);
        Assert.Equal("MyProcess", loaded.Name);
    }

    [Fact]
    public async Task FlowService_GetDefinitionsAsync_MergesMemoryAndRepository()
    {
        var repo = new InMemoryDefinitionRepository();
        var processRepo = new InMemoryProcessRepository();

        var defInRepo = CreateTestDefinition("RepoProcess", "1.0");
        await repo.SaveDefinitionAsync(defInRepo);

        var defInMemory = CreateTestDefinition("MemoryProcess", "1.0");

        var service = new FlowService(
            definitions: new[] { defInMemory },
            repository: processRepo,
            handlers: Array.Empty<SimpleBPM.Handlers.INodeHandler>(),
            definitionRepository: repo);

        var all = await service.GetDefinitionsAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, d => d.Name == "RepoProcess");
        Assert.Contains(all, d => d.Name == "MemoryProcess");
    }

    [Fact]
    public async Task FlowService_SaveDefinition_WithoutRepository_Throws()
    {
        var processRepo = new InMemoryProcessRepository();
        var service = new FlowService(
            definitions: Array.Empty<ProcessDefinition>(),
            repository: processRepo,
            handlers: Array.Empty<SimpleBPM.Handlers.INodeHandler>());

        var def = CreateTestDefinition();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDefinitionAsync(def));
    }
}
