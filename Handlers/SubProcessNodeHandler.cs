using SimpleBPM.Abstractions;
using SimpleBPM.Nodes;
using SimpleBPM.Persistence;

namespace SimpleBPM.Handlers;

public class SubProcessNodeHandler : INodeHandler
{
    private readonly IProcessRepository? _repository;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;
    private readonly IProcessEventHandler? _eventHandler;

    public NodeType NodeType => NodeType.SubProcess;

    internal SubProcessNodeHandler(IProcessRepository? repository, Dictionary<NodeType, INodeHandler> handlers, IProcessEventHandler? eventHandler = null)
    {
        _repository = repository;
        _handlers = handlers;
        _eventHandler = eventHandler;
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var subNode = (SubProcessNode)node;

        try
        {
            // Check if a subprocess already exists (resume after stop)
            instance.SubProcessIds.TryGetValue(node.Id, out var existingSubProcessId);

            ProcessInstance subInstance;
            string subProcessId;

            if (!string.IsNullOrEmpty(existingSubProcessId) && _repository != null)
            {
                // Resume an existing subprocess
                var loadedInstance = await _repository.GetProcessInstanceAsync(existingSubProcessId);

                if (loadedInstance == null)
                {
                    return new NodeExecutionResult
                    {
                        IsCompleted = false,
                        ErrorMessage = $"Sub-process {existingSubProcessId} not found"
                    };
                }

                subInstance = loadedInstance;
                subProcessId = existingSubProcessId;
            }
            else
            {
                // Create a new subprocess with parent link
                subProcessId = $"{instance.ProcessId}_SUB_{node.Id}_{Guid.NewGuid():N}";
                subInstance = new ProcessInstance(
                    subProcessId,
                    subNode.InheritAggregateId ? instance.AggregateId : null
                )
                {
                    ParentProcessId = instance.ProcessId,
                    ParentNodeId = node.Id
                };

                // Apply input mapping: parent variables -> subprocess variables
                foreach (var mapping in subNode.InputMapping)
                {
                    if (instance.Variables.TryGetValue(mapping.Key, out var value))
                    {
                        subInstance.Variables[mapping.Value] = value;
                    }
                }
            }

            // Create engine for the subprocess sharing handlers
            var subEngine = new FlowEngine(subNode.SubProcessDefinition, _repository, _handlers);

            // Execute or continue the subprocess
            if (string.IsNullOrEmpty(existingSubProcessId))
            {
                subInstance = await subEngine.ExecuteAsync(subInstance);
            }
            else
            {
                subInstance = await subEngine.ContinueAsync(subInstance);
            }

            // Check subprocess status
            if (subInstance.Status == ProcessStatus.Failed)
            {
                return new NodeExecutionResult
                {
                    IsCompleted = false,
                    ErrorMessage = $"Sub-process failed at node {subInstance.CurrentNodeId}"
                };
            }

            // If the subprocess is waiting, save its state and pause the parent
            if (subInstance.Status == ProcessStatus.WaitingInteraction ||
                subInstance.Status == ProcessStatus.WaitingDate ||
                subInstance.Status == ProcessStatus.WaitingSignal)
            {
                instance.SubProcessIds[node.Id] = subProcessId;

                return new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                };
            }

            // Subprocess completed - propagate output variables back to parent
            var outputVariables = ExtractOutputVariables(subInstance, subNode);
            PropagateToParent(instance, outputVariables);

            // Notify that subprocess completed
            if (_eventHandler != null && subInstance.ParentProcessId != null)
            {
                await _eventHandler.OnSubProcessCompletedAsync(new SubProcessCompletedEvent
                {
                    SubProcessId = subInstance.ProcessId,
                    ParentProcessId = subInstance.ParentProcessId,
                    ParentNodeId = subInstance.ParentNodeId ?? node.Id,
                    OutputData = outputVariables,
                    SubProcessStatus = subInstance.Status,
                    CompletedAt = subInstance.CompletedAt ?? DateTime.UtcNow
                });
            }

            // Clean up subprocess tracking
            instance.SubProcessIds.Remove(node.Id);

            if (_repository != null && !string.IsNullOrEmpty(existingSubProcessId))
            {
                await _repository.DeleteProcessInstanceAsync(existingSubProcessId);
            }

            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = node.NextNodeIds.FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                IsCompleted = false,
                ErrorMessage = $"Sub-process execution error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extracts the mapped output variables from a completed subprocess.
    /// </summary>
    private static Dictionary<string, object> ExtractOutputVariables(ProcessInstance subInstance, SubProcessNode subNode)
    {
        var output = new Dictionary<string, object>();
        foreach (var mapping in subNode.OutputMapping)
        {
            if (subInstance.Variables.TryGetValue(mapping.Key, out var value))
            {
                output[mapping.Value] = value;
            }
        }
        return output;
    }

    /// <summary>
    /// Applies the mapped output variables to the parent process instance.
    /// </summary>
    private static void PropagateToParent(ProcessInstance parentInstance, Dictionary<string, object> outputVariables)
    {
        foreach (var kvp in outputVariables)
        {
            parentInstance.Variables[kvp.Key] = kvp.Value;
        }
    }
}
