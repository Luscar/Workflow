using SimpleBPM;
using SimpleBPM.Handlers;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;
using NSubstitute;

namespace SimpleBPM.Tests;

public class SubProcessNodeTests
{
    [Fact]
    public void Constructor_SetsSubProcessDefinition()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef);

        Assert.Equal(subDef, node.SubProcessDefinition);
        Assert.Equal(NodeType.SubProcess, node.Type);
    }

    [Fact]
    public void InheritAggregateId_DefaultsToTrue()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef);

        Assert.True(node.InheritAggregateId);
    }

    [Fact]
    public void InheritAggregateId_CanBeSetToFalse()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef) { InheritAggregateId = false };

        Assert.False(node.InheritAggregateId);
    }

    [Fact]
    public void InputMapping_DefaultsToEmpty()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef);

        Assert.NotNull(node.InputMapping);
        Assert.Empty(node.InputMapping);
    }

    [Fact]
    public void OutputMapping_DefaultsToEmpty()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef);

        Assert.NotNull(node.OutputMapping);
        Assert.Empty(node.OutputMapping);
    }

    [Fact]
    public void InputMapping_CanBeConfigured()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef)
        {
            InputMapping = new Dictionary<string, string>
            {
                ["parentVar"] = "childVar",
                ["orderId"] = "subOrderId"
            }
        };

        Assert.Equal(2, node.InputMapping.Count);
        Assert.Equal("childVar", node.InputMapping["parentVar"]);
        Assert.Equal("subOrderId", node.InputMapping["orderId"]);
    }

    [Fact]
    public void OutputMapping_CanBeConfigured()
    {
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var node = new SubProcessNode(subDef)
        {
            OutputMapping = new Dictionary<string, string>
            {
                ["subResult"] = "parentResult"
            }
        };

        Assert.Single(node.OutputMapping);
        Assert.Equal("parentResult", node.OutputMapping["subResult"]);
    }
}

public class SubProcessNodeHandlerTests
{
    private static ProcessDefinition CreateSimpleSubProcessDef()
    {
        var def = new ProcessDefinition("SubProcess", "1.0");
        var node1 = new BusinessNode("SubStep1") { Name = "SubStep1", DisplayName = "Sub Step 1" };
        var node2 = new BusinessNode("SubStep2") { Name = "SubStep2", DisplayName = "Sub Step 2" };
        node1.NextNodeIds.Add("SubStep2");
        def.AddNode(node1);
        def.AddNode(node2);
        return def;
    }

    private static INodeHandler CreateSuccessfulBusinessHandler()
    {
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<NodeDefinition>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });
        return handler;
    }

    private static SubProcessNodeHandler CreateHandler(
        IProcessRepository? repository = null,
        Dictionary<NodeType, INodeHandler>? handlers = null)
    {
        var repo = repository ?? Substitute.For<IProcessRepository>();
        if (repository == null)
        {
            repo.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
            repo.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);
        }

        var handlerDict = handlers ?? new Dictionary<NodeType, INodeHandler>();
        if (!handlerDict.ContainsKey(NodeType.Business))
        {
            handlerDict[NodeType.Business] = CreateSuccessfulBusinessHandler();
        }

        return new SubProcessNodeHandler(repo, handlerDict);
    }

    [Fact]
    public void NodeType_IsSubProcess()
    {
        var handler = CreateHandler();
        Assert.Equal(NodeType.SubProcess, handler.NodeType);
    }

    [Fact]
    public async Task HandleAsync_SimpleSubProcess_CompletesSuccessfully()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        subNode.NextNodeIds.Add("NextParentNode");

        var parentInstance = new ProcessInstance(1, 1L);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("NextParentNode", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_InheritsAggregateId_WhenEnabled()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        ProcessInstance? savedInstance = null;
        repository.SaveProcessInstanceAsync(Arg.Do<ProcessInstance>(i => savedInstance = i));

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub", InheritAggregateId = true };

        var parentInstance = new ProcessInstance(1, 123L);

        await handler.HandleAsync(subNode, parentInstance);

        Assert.NotNull(savedInstance);
        Assert.Equal(123L, savedInstance!.AggregateId);
        Assert.Equal(1L, savedInstance.ParentProcessId);
        Assert.Equal("RunSub", savedInstance.ParentNodeId);
    }

    [Fact]
    public async Task HandleAsync_DoesNotInheritAggregateId_WhenDisabled()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        ProcessInstance? savedInstance = null;
        repository.SaveProcessInstanceAsync(Arg.Do<ProcessInstance>(i => savedInstance = i));

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub", InheritAggregateId = false };

        var parentInstance = new ProcessInstance(1, 123L);

        await handler.HandleAsync(subNode, parentInstance);

        Assert.NotNull(savedInstance);
        Assert.Null(savedInstance!.AggregateId);
    }

    [Fact]
    public async Task HandleAsync_InputMapping_CopiesVariablesToSubProcess()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        ProcessInstance? savedInstance = null;
        repository.SaveProcessInstanceAsync(Arg.Do<ProcessInstance>(i => savedInstance = i));

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef)
        {
            Name = "RunSub",
            DisplayName = "Run Sub",
            InputMapping = new Dictionary<string, string>
            {
                ["parentOrderId"] = "orderId",
                ["parentAmount"] = "amount"
            }
        };

        var parentInstance = new ProcessInstance(1);
        parentInstance.Variables["parentOrderId"] = "ORD-001";
        parentInstance.Variables["parentAmount"] = 150.0;
        parentInstance.Variables["notMapped"] = "ignored";

        await handler.HandleAsync(subNode, parentInstance);

        Assert.NotNull(savedInstance);
        Assert.Equal("ORD-001", savedInstance!.Variables["orderId"]);
        Assert.Equal(150.0, savedInstance.Variables["amount"]);
        Assert.False(savedInstance.Variables.ContainsKey("notMapped"));
        Assert.False(savedInstance.Variables.ContainsKey("parentOrderId"));
    }

    [Fact]
    public async Task HandleAsync_InputMapping_SkipsMissingVariables()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        ProcessInstance? savedInstance = null;
        repository.SaveProcessInstanceAsync(Arg.Do<ProcessInstance>(i => savedInstance = i));

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef)
        {
            Name = "RunSub",
            DisplayName = "Run Sub",
            InputMapping = new Dictionary<string, string>
            {
                ["exists"] = "mapped",
                ["missing"] = "notSet"
            }
        };

        var parentInstance = new ProcessInstance(1);
        parentInstance.Variables["exists"] = "value";

        await handler.HandleAsync(subNode, parentInstance);

        Assert.NotNull(savedInstance);
        Assert.Equal("value", savedInstance!.Variables["mapped"]);
        Assert.False(savedInstance.Variables.ContainsKey("notSet"));
    }

    [Fact]
    public async Task HandleAsync_OutputMapping_CopiesVariablesBackToParent()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        // Create a business handler that sets a result variable on the subprocess
        var businessHandler = Substitute.For<INodeHandler>();
        businessHandler.NodeType.Returns(NodeType.Business);
        businessHandler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<NodeDefinition>(0);
                var inst = callInfo.ArgAt<ProcessInstance>(1);
                inst.Variables["subResult"] = "processed";
                inst.Variables["subTotal"] = 200.0;
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.Business] = businessHandler
        };

        var handler = CreateHandler(repository, handlers);

        var subDef = new ProcessDefinition("SubProcess", "1.0");
        subDef.AddNode(new BusinessNode("Final") { Name = "Final", DisplayName = "Final" });

        var subNode = new SubProcessNode(subDef)
        {
            Name = "RunSub",
            DisplayName = "Run Sub",
            OutputMapping = new Dictionary<string, string>
            {
                ["subResult"] = "parentResult",
                ["subTotal"] = "parentTotal"
            }
        };

        var parentInstance = new ProcessInstance(1);

        await handler.HandleAsync(subNode, parentInstance);

        Assert.Equal("processed", parentInstance.Variables["parentResult"]);
        Assert.Equal(200.0, parentInstance.Variables["parentTotal"]);
    }

    [Fact]
    public async Task HandleAsync_SubProcessWithInteractiveNode_StopsWithRequiresStop()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.Interactive] = new InteractiveNodeHandler()
        };

        var handler = CreateHandler(repository, handlers);

        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var interactiveNode = new InteractiveNode { Name = "SubReview", DisplayName = "Sub Review" };
        interactiveNode.NextNodeIds.Add("SubFinal");
        subDef.AddNode(interactiveNode);
        subDef.AddNode(new BusinessNode("SubFinalCmd") { Name = "SubFinal", DisplayName = "Sub Final" });

        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        subNode.NextNodeIds.Add("NextParentNode");

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
        Assert.Equal("NextParentNode", result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_SubProcessWithWaitForSignal_StopsWithRequiresStop()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.WaitForSignal] = new WaitForSignalNodeHandler()
        };

        var handler = CreateHandler(repository, handlers);

        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var signalNode = new WaitForSignalNode("my-signal") { Name = "WaitSig", DisplayName = "Wait Signal" };
        signalNode.NextNodeIds.Add("SubFinal");
        subDef.AddNode(signalNode);
        subDef.AddNode(new BusinessNode("SubFinalCmd") { Name = "SubFinal", DisplayName = "Sub Final" });

        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        subNode.NextNodeIds.Add("NextParent");

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_SubProcessWithWaitUntilDate_StopsWithRequiresStop()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.WaitUntilDate] = new WaitUntilDateNodeHandler()
        };

        var handler = CreateHandler(repository, handlers);

        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var waitNode = new WaitUntilDateNode(DateTime.UtcNow.AddDays(5))
        {
            Name = "WaitDate",
            DisplayName = "Wait Date"
        };
        waitNode.NextNodeIds.Add("SubFinal");
        subDef.AddNode(waitNode);
        subDef.AddNode(new BusinessNode("SubFinalCmd") { Name = "SubFinal", DisplayName = "Sub Final" });

        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        subNode.NextNodeIds.Add("NextParent");

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.True(result.RequiresStop);
    }

    [Fact]
    public async Task HandleAsync_FailedSubProcess_ReturnsError()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var failHandler = Substitute.For<INodeHandler>();
        failHandler.NodeType.Returns(NodeType.Business);
        failHandler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(Task.FromResult(new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = "Step failed"
            }));

        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.Business] = failHandler
        };

        var handler = CreateHandler(repository, handlers);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.False(result.IsCompleted);
        Assert.Contains("Sub-process failed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ExistingChildProcess_ResumesExecution()
    {
        var existingChild = new ProcessInstance(100)
        {
            ParentProcessId = 1,
            ParentNodeId = "RunSub",
            Status = ProcessStatus.WaitingInteraction,
            CurrentNodeId = "SubFinal",
            DefinitionName = "SubProcess",
            DefinitionVersion = "1.0"
        };

        var repository = Substitute.For<IProcessRepository>();
        repository.GetChildProcessAsync(1, "RunSub").Returns(existingChild);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var handlers = new Dictionary<NodeType, INodeHandler>
        {
            [NodeType.Business] = businessHandler,
            [NodeType.Interactive] = new InteractiveNodeHandler()
        };

        var handler = CreateHandler(repository, handlers);

        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var interactiveNode = new InteractiveNode { Name = "SubReview", DisplayName = "Sub Review" };
        interactiveNode.NextNodeIds.Add("SubFinal");
        var finalNode = new BusinessNode("SubFinalCmd") { Name = "SubFinal", DisplayName = "Sub Final" };
        subDef.AddNode(interactiveNode);
        subDef.AddNode(finalNode);

        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        subNode.NextNodeIds.Add("NextParent");

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.False(result.RequiresStop);
        Assert.Equal("NextParent", result.NextNodeId);

        // Verify child process was cleaned up
        await repository.Received(1).DeleteProcessInstanceAsync(100);
    }

    [Fact]
    public async Task HandleAsync_NewChildProcess_DoesNotDeleteOnCompletion()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };

        var parentInstance = new ProcessInstance(1);

        await handler.HandleAsync(subNode, parentInstance);

        // New child processes don't trigger deletion on completion (only resumed ones do)
        await repository.DidNotReceive().DeleteProcessInstanceAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task HandleAsync_GetsSequenceId_ForNewSubProcess()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync("SEQ_PROCESSUS").Returns(42L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };

        var parentInstance = new ProcessInstance(1);

        await handler.HandleAsync(subNode, parentInstance);

        await repository.Received(1).ObtenirSequenceAsync("SEQ_PROCESSUS");
    }

    [Fact]
    public async Task HandleAsync_Exception_ReturnsError()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("Database connection failed"));

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.False(result.IsCompleted);
        Assert.Contains("Sub-process execution error", result.ErrorMessage);
        Assert.Contains("Database connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_NoNextNode_CompletesWithNullNextNodeId()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        // SubNode with NO NextNodeIds
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.Null(result.NextNodeId);
    }

    [Fact]
    public async Task HandleAsync_OutputMapping_SkipsMissingSubProcessVariables()
    {
        var repository = Substitute.For<IProcessRepository>();
        repository.ObtenirSequenceAsync(Arg.Any<string>()).Returns(100L);
        repository.GetChildProcessAsync(Arg.Any<long>(), Arg.Any<string>()).Returns((ProcessInstance?)null);

        var handler = CreateHandler(repository);

        var subDef = CreateSimpleSubProcessDef();
        var subNode = new SubProcessNode(subDef)
        {
            Name = "RunSub",
            DisplayName = "Run Sub",
            OutputMapping = new Dictionary<string, string>
            {
                ["missingVar"] = "parentVar"
            }
        };

        var parentInstance = new ProcessInstance(1);

        var result = await handler.HandleAsync(subNode, parentInstance);

        Assert.True(result.IsCompleted);
        Assert.False(parentInstance.Variables.ContainsKey("parentVar"));
    }
}

public class SubProcessIntegrationTests
{
    private static INodeHandler CreateSuccessfulBusinessHandler()
    {
        var handler = Substitute.For<INodeHandler>();
        handler.NodeType.Returns(NodeType.Business);
        handler.HandleAsync(Arg.Any<NodeDefinition>(), Arg.Any<ProcessInstance>())
            .Returns(callInfo =>
            {
                var node = callInfo.ArgAt<NodeDefinition>(0);
                return Task.FromResult(new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = false,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                });
            });
        return handler;
    }

    [Fact]
    public async Task FlowEngine_ExecutesSubProcessNode_EndToEnd()
    {
        // Create subprocess definition
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var subStep = new BusinessNode("SubCmd") { Name = "SubStep", DisplayName = "Sub Step" };
        subDef.AddNode(subStep);

        // Create parent definition with subprocess node
        var parentDef = new ProcessDefinition("ParentProcess", "1.0");
        var step1 = new BusinessNode("Step1Cmd") { Name = "Step1", DisplayName = "Step 1" };
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        var step3 = new BusinessNode("Step3Cmd") { Name = "Step3", DisplayName = "Step 3" };
        step1.NextNodeIds.Add("RunSub");
        subNode.NextNodeIds.Add("Step3");
        parentDef.AddNode(step1);
        parentDef.AddNode(subNode);
        parentDef.AddNode(step3);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { parentDef }, handlers: new[] { businessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "ParentProcess" };
        var result = await engine.ExecuteAsync(instance);

        Assert.Equal(ProcessStatus.Completed, result.Status);
    }

    [Fact]
    public async Task FlowEngine_SubProcessWithInteractive_StopsParentProcess()
    {
        // Create subprocess with interactive node
        var subDef = new ProcessDefinition("SubProcess", "1.0");
        var interactiveNode = new InteractiveNode { Name = "SubReview", DisplayName = "Sub Review" };
        subDef.AddNode(interactiveNode);

        // Create parent definition
        var parentDef = new ProcessDefinition("ParentProcess", "1.0");
        var subNode = new SubProcessNode(subDef) { Name = "RunSub", DisplayName = "Run Sub" };
        var finalStep = new BusinessNode("FinalCmd") { Name = "Final", DisplayName = "Final" };
        subNode.NextNodeIds.Add("Final");
        parentDef.AddNode(subNode);
        parentDef.AddNode(finalStep);

        var businessHandler = CreateSuccessfulBusinessHandler();
        var engine = new FlowEngine(new[] { parentDef }, handlers: new[] { businessHandler });

        var instance = new ProcessInstance(1) { DefinitionName = "ParentProcess" };
        var result = await engine.ExecuteAsync(instance);

        // Parent should be stopped because subprocess hit an interactive node
        Assert.NotEqual(ProcessStatus.Completed, result.Status);
    }
}
