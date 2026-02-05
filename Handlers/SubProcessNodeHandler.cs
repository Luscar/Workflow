using SimpleBPM.Nodes;
using SimpleBPM.Persistence;

namespace SimpleBPM.Handlers;

public class SubProcessNodeHandler : INodeHandler
{
    private readonly IProcessRepository? _repository;
    private readonly Dictionary<NodeType, INodeHandler> _handlers;

    public NodeType NodeType => NodeType.SubProcess;

    internal SubProcessNodeHandler(IProcessRepository? repository, Dictionary<NodeType, INodeHandler> handlers)
    {
        _repository = repository;
        _handlers = handlers;
    }

    public async Task<NodeExecutionResult> HandleAsync(ProcessNode node, ProcessInstance instance)
    {
        var subNode = (SubProcessNode)node;

        try
        {
            // Vérifier si un sous-processus existe déjà (reprise après arrêt)
            var existingSubProcess = _repository != null
                ? await _repository.GetChildProcessAsync(instance.ProcessId, node.Id)
                : null;

            ProcessInstance subInstance;

            if (existingSubProcess != null)
            {
                // Reprendre un sous-processus existant
                subInstance = existingSubProcess;
            }
            else
            {
                // Créer un nouveau sous-processus
                var subProcessId = Random.Shared.NextInt64(1, 10_000_000_000L);
                subInstance = new ProcessInstance(
                    subProcessId,
                    subNode.InheritAggregateId ? instance.AggregateId : null
                )
                {
                    ParentProcessId = instance.ProcessId,
                    ParentNodeId = node.Id
                };

                // Copier les variables d'entrée via le mapping explicite
                foreach (var mapping in subNode.InputMapping)
                {
                    if (instance.Variables.TryGetValue(mapping.Key, out var value))
                    {
                        subInstance.Variables[mapping.Value] = value;
                    }
                }
            }

            // Créer un moteur pour le sous-processus avec le même repository et handlers
            var subEngine = new FlowEngine(subNode.SubProcessDefinition, _repository, _handlers);

            // Exécuter ou continuer le sous-processus
            if (existingSubProcess == null)
            {
                subInstance = await subEngine.ExecuteAsync(subInstance);
            }
            else
            {
                subInstance = await subEngine.ContinueAsync(subInstance);
            }

            // Vérifier le statut du sous-processus
            if (subInstance.Status == ProcessStatus.Failed)
            {
                return new NodeExecutionResult
                {
                    IsCompleted = false,
                    ErrorMessage = $"Sub-process failed at node {subInstance.CurrentNodeId}"
                };
            }

            // Si le sous-processus est en attente, sauvegarder son état
            if (subInstance.Status == ProcessStatus.WaitingInteraction ||
                subInstance.Status == ProcessStatus.WaitingDate ||
                subInstance.Status == ProcessStatus.WaitingSignal)
            {
                return new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = true,
                    NextNodeId = node.NextNodeIds.FirstOrDefault()
                };
            }

            // Le sous-processus est complété
            // Récupérer les variables de sortie via le mapping explicite
            foreach (var mapping in subNode.OutputMapping)
            {
                if (subInstance.Variables.TryGetValue(mapping.Key, out var value))
                {
                    instance.Variables[mapping.Value] = value;
                }
            }

            // Supprimer le contexte du sous-processus de la base de données
            if (_repository != null && existingSubProcess != null)
            {
                await _repository.DeleteProcessInstanceAsync(subInstance.ProcessId);
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
}
