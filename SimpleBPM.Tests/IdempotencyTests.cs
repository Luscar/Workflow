using SimpleBPM;
using SimpleBPM.Abstractions;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;
using NSubstitute;

namespace SimpleBPM.Tests;

/// <summary>
/// Verifies the three idempotency guarantees between the messaging layer and the BPM engine:
///
///  1. Process-creation deduplication – same idempotency key produces only one process instance.
///  2. Node-execution deduplication   – a node that already completed successfully is skipped
///     on re-execution (e.g. after a crash or a duplicate message delivery).
///  3. Signal deduplication           – a duplicate signal is silently ignored instead of throwing.
/// </summary>
public class IdempotencyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ProcessDefinition CreateTwoStepProcess(string name = "TestProcess")
    {
        var def = new ProcessDefinition(name, "1.0");
        var step1 = new BusinessNode("Step1") { Name = "Step1", DisplayName = "Step 1" };
        var step2 = new BusinessNode("Step2") { Name = "Step2", DisplayName = "Step 2" };
        step1.NextNodeIds.Add("Step2");
        def.AddNode(step1);
        def.AddNode(step2);
        def.StartNodeId = "Step1";
        return def;
    }

    private static ProcessDefinition CreateSignalProcess()
    {
        var def = new ProcessDefinition("SignalProcess", "1.0");
        var step1 = new BusinessNode("BeforeSignal") { Name = "BeforeSignal", DisplayName = "Before Signal" };
        var wait = new WaitForSignalNode("WaitPayment") { Name = "WaitPayment", DisplayName = "Wait Payment", SignalName = "payment-received" };
        var step2 = new BusinessNode("AfterSignal") { Name = "AfterSignal", DisplayName = "After Signal" };
        step1.NextNodeIds.Add("WaitPayment");
        wait.NextNodeIds.Add("AfterSignal");
        def.AddNode(step1);
        def.AddNode(wait);
        def.AddNode(step2);
        def.StartNodeId = "BeforeSignal";
        return def;
    }

    private static INodeHandler CreateSuccessfulBusinessHandler()
    {
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });
        return handler;
    }

    // -------------------------------------------------------------------------
    // 1. Process-creation deduplication
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateProcessInstanceIdempotentAsync_SameKey_ReturnsExistingProcessId()
    {
        var repo = new InMemoryProcessRepository();
        var def = CreateTwoStepProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var service = new FlowService(new[] { def }, repo, new[] { handler });

        const string key = "order-created:abc-123";

        var id1 = await service.CreateProcessInstanceIdempotentAsync(key, "TestProcess");
        var id2 = await service.CreateProcessInstanceIdempotentAsync(key, "TestProcess");

        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task CreateProcessInstanceIdempotentAsync_DifferentKeys_CreateSeparateProcesses()
    {
        var repo = new InMemoryProcessRepository();
        var def = CreateTwoStepProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var service = new FlowService(new[] { def }, repo, new[] { handler });

        var id1 = await service.CreateProcessInstanceIdempotentAsync("key-A", "TestProcess");
        var id2 = await service.CreateProcessInstanceIdempotentAsync("key-B", "TestProcess");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task CreateProcessInstanceIdempotentAsync_DuplicateDelivery_ExecutesHandlerOnlyOnce()
    {
        var repo = new InMemoryProcessRepository();
        var def = CreateTwoStepProcess();

        var executionCount = 0;
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                executionCount++;
                var node = callInfo.ArgAt<ProcessNode>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var service = new FlowService(new[] { def }, repo, new[] { handler });

        const string key = "msg-duplicate-test";
        await service.CreateProcessInstanceIdempotentAsync(key, "TestProcess");
        await service.CreateProcessInstanceIdempotentAsync(key, "TestProcess");

        // 2 nodes × 1 execution each (second call returns early before the engine runs)
        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task CreateProcessInstanceIdempotentAsync_StoresIdempotencyKey()
    {
        var repo = new InMemoryProcessRepository();
        var def = CreateTwoStepProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var service = new FlowService(new[] { def }, repo, new[] { handler });

        const string key = "my-idempotency-key";
        var id = await service.CreateProcessInstanceIdempotentAsync(key, "TestProcess");

        var stored = await repo.GetByIdempotencyKeyAsync(key);
        Assert.NotNull(stored);
        Assert.Equal(id, stored!.ProcessId);
        Assert.Equal(key, stored.IdempotencyKey);
    }

    // -------------------------------------------------------------------------
    // 2. Node-execution deduplication (FlowEngine history-based skip)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NodeAlreadySucceeded_SkipsNodeOnReplay()
    {
        var def = CreateTwoStepProcess();

        var executionCount = 0;
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                executionCount++;
                var node = callInfo.ArgAt<ProcessNode>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        // Pre-populate execution history as if Step1 already ran successfully.
        var instance = new ProcessInstance(1) { DefinitionName = "TestProcess" };
        var alreadyRan = new NodeExecutionHistory("Step1", "Step 1", NodeType.Business);
        alreadyRan.Complete(success: true, nextNodeId: "Step2");
        instance.ExecutionHistory.Add(alreadyRan);

        await engine.ExecuteAsync(instance);

        // Step1 should have been skipped; only Step2 runs.
        Assert.Equal(1, executionCount);
        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task ExecuteAsync_NodeFailedPreviously_DoesNotSkipRetry()
    {
        var def = CreateTwoStepProcess();

        var executionCount = 0;
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                executionCount++;
                var node = callInfo.ArgAt<ProcessNode>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        // Pre-populate history with a failed Step1 — this should NOT be skipped.
        var instance = new ProcessInstance(1) { DefinitionName = "TestProcess" };
        var failedEntry = new NodeExecutionHistory("Step1", "Step 1", NodeType.Business);
        failedEntry.Complete(success: false, errorMessage: "transient error");
        instance.ExecutionHistory.Add(failedEntry);

        await engine.ExecuteAsync(instance);

        // Step1 ran again (retry), then Step2 ran → 2 total.
        Assert.Equal(2, executionCount);
        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task ExecuteAsync_BothNodesAlreadySucceeded_CompletesWithoutCallingHandler()
    {
        var def = CreateTwoStepProcess();

        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);

        var engine = new FlowEngine(new[] { def }, handlers: new[] { handler });

        var instance = new ProcessInstance(1) { DefinitionName = "TestProcess" };

        var h1 = new NodeExecutionHistory("Step1", "Step 1", NodeType.Business);
        h1.Complete(success: true, nextNodeId: "Step2");
        var h2 = new NodeExecutionHistory("Step2", "Step 2", NodeType.Business);
        h2.Complete(success: true, nextNodeId: null);
        instance.ExecutionHistory.Add(h1);
        instance.ExecutionHistory.Add(h2);

        await engine.ExecuteAsync(instance);

        await handler.DidNotReceive().HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>());
        Assert.Equal(ProcessStatus.Completed, instance.Status);
    }

    // -------------------------------------------------------------------------
    // 3. Signal deduplication
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnvoyerSignalAsync_DuplicateSignal_DoesNotThrow()
    {
        var repo = new InMemoryProcessRepository();
        var def = CreateSignalProcess();
        var handler = CreateSuccessfulBusinessHandler();
        var service = new FlowService(new[] { def }, repo, new[] { handler });

        var processId = await service.CreateProcessInstance("SignalProcess");

        // First signal — should process normally.
        await service.EnvoyerSignalAsync(processId, "payment-received");

        // Second signal — process is no longer WaitingSignal; should be a no-op, not an exception.
        var ex = await Record.ExceptionAsync(() => service.EnvoyerSignalAsync(processId, "payment-received"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task EnvoyerSignalAsync_SignalProcessed_ProcessCompletesExactlyOnce()
    {
        var repo = new InMemoryProcessRepository();
        var def = CreateSignalProcess();

        var afterSignalCount = 0;
        var businessHandler = Substitute.For<INodeHandler>();
        businessHandler.NodeType.Returns(NodeType.Business);
        businessHandler.HandleAsync(Arg.Any<ProcessNode>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<ProcessNode>(0);
                if (node.Name == "AfterSignal")
                    afterSignalCount++;
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var service = new FlowService(new[] { def }, repo, new[] { businessHandler });
        var processId = await service.CreateProcessInstance("SignalProcess");

        // Send the signal twice (simulating at-least-once messaging).
        await service.EnvoyerSignalAsync(processId, "payment-received");
        await service.EnvoyerSignalAsync(processId, "payment-received");

        Assert.Equal(1, afterSignalCount);

        var process = await service.ObtenirAsync(processId);
        Assert.Equal(ProcessStatus.Completed, process.Status);
    }
}
