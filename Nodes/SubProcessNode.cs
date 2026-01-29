namespace SimpleBPM.Nodes;

public class SubProcessNode : ProcessNode
{
    public ProcessDefinition SubProcessDefinition { get; set; }
    public bool InheritAggregateId { get; set; } = true;

    public SubProcessNode(ProcessDefinition subProcessDefinition) : base(NodeType.SubProcess)
    {
        SubProcessDefinition = subProcessDefinition;
    }

    public override async Task<NodeExecutionResult> ExecuteAsync(ProcessInstance instance, Persistence.IProcessRepository? repository = null)
    {
        try
        {
            // Vérifier si un sous-processus existe déjà (reprise après arrêt)
            var existingSubProcessId = instance.Data.ContainsKey($"SUB_PROCESS_{Id}")
                ? instance.Data[$"SUB_PROCESS_{Id}"]?.ToString()
                : null;

            ProcessInstance subInstance;
            string subProcessId;

            if (!string.IsNullOrEmpty(existingSubProcessId) && repository != null)
            {
                // Reprendre un sous-processus existant
                var loadedInstance = await repository.GetProcessInstanceAsync(existingSubProcessId);

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
                // Créer un nouveau sous-processus
                subProcessId = $"{instance.ProcessId}_SUB_{Id}_{Guid.NewGuid():N}";
                subInstance = new ProcessInstance(
                    subProcessId,
                    InheritAggregateId ? instance.AggregateId : null
                );

                // Copier les données du contexte parent marquées pour le sous-processus
                foreach (var kvp in instance.Data)
                {
                    if (kvp.Key.StartsWith("SUB_INPUT_"))
                    {
                        var keyName = kvp.Key.Replace("SUB_INPUT_", "");
                        subInstance.Data[keyName] = kvp.Value;
                    }
                }
            }

            // Créer un moteur pour le sous-processus avec le même repository
            var subEngine = new ProcessEngine(SubProcessDefinition, repository);

            // Exécuter ou continuer le sous-processus
            if (string.IsNullOrEmpty(existingSubProcessId))
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
                // Sauvegarder l'ID du sous-processus dans le contexte parent
                instance.Data[$"SUB_PROCESS_{Id}"] = subProcessId;

                return new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = true,
                    NextNodeId = NextNodeIds.FirstOrDefault()
                };
            }

            // Le sous-processus est complété
            // Récupérer les données de sortie du sous-processus
            foreach (var kvp in subInstance.Data)
            {
                if (kvp.Key.StartsWith("OUTPUT_"))
                {
                    var keyName = kvp.Key.Replace("OUTPUT_", "SUB_OUTPUT_");
                    instance.Data[keyName] = kvp.Value;
                }
            }

            // Nettoyer les données du sous-processus
            instance.Data.Remove($"SUB_PROCESS_{Id}");

            // Supprimer le contexte du sous-processus de la base de données
            if (repository != null && !string.IsNullOrEmpty(existingSubProcessId))
            {
                await repository.DeleteProcessInstanceAsync(existingSubProcessId);
            }

            return new NodeExecutionResult
            {
                IsCompleted = true,
                RequiresStop = false,
                NextNodeId = NextNodeIds.FirstOrDefault()
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
