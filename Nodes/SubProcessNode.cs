namespace SimpleBPM.Nodes;

public class SubProcessNode : ProcessNode
{
    public ProcessDefinition SubProcessDefinition { get; set; }
    public bool InheritAggregateId { get; set; } = true;

    public SubProcessNode(ProcessDefinition subProcessDefinition) : base(NodeType.SubProcess)
    {
        SubProcessDefinition = subProcessDefinition;
    }

    public override async Task<NodeExecutionResult> ExecuteAsync(ProcessContext context, Persistence.IProcessRepository? repository = null)
    {
        try
        {
            // Vérifier si un sous-processus existe déjà (reprise après arrêt)
            var existingSubProcessId = context.Data.ContainsKey($"SUB_PROCESS_{Id}")
                ? context.Data[$"SUB_PROCESS_{Id}"]?.ToString()
                : null;

            ProcessContext subContext;
            string subProcessId;

            if (!string.IsNullOrEmpty(existingSubProcessId) && repository != null)
            {
                // Reprendre un sous-processus existant
                var loadedContext = await repository.GetProcessContextAsync(existingSubProcessId);

                if (loadedContext == null)
                {
                    return new NodeExecutionResult
                    {
                        IsCompleted = false,
                        ErrorMessage = $"Sub-process {existingSubProcessId} not found"
                    };
                }

                subContext = loadedContext;
                subProcessId = existingSubProcessId;
            }
            else
            {
                // Créer un nouveau sous-processus
                subProcessId = $"{context.ProcessId}_SUB_{Id}_{Guid.NewGuid():N}";
                subContext = new ProcessContext(
                    subProcessId,
                    InheritAggregateId ? context.AggregateId : null
                );

                // Copier les données du contexte parent marquées pour le sous-processus
                foreach (var kvp in context.Data)
                {
                    if (kvp.Key.StartsWith("SUB_INPUT_"))
                    {
                        var keyName = kvp.Key.Replace("SUB_INPUT_", "");
                        subContext.Data[keyName] = kvp.Value;
                    }
                }
            }

            // Créer un moteur pour le sous-processus avec le même repository
            var subEngine = new ProcessEngine(SubProcessDefinition, repository);

            // Exécuter ou continuer le sous-processus
            if (string.IsNullOrEmpty(existingSubProcessId))
            {
                subContext = await subEngine.ExecuteAsync(subContext);
            }
            else
            {
                subContext = await subEngine.ContinueAsync(subContext);
            }

            // Vérifier le statut du sous-processus
            if (subContext.Status == ProcessStatus.Failed)
            {
                return new NodeExecutionResult
                {
                    IsCompleted = false,
                    ErrorMessage = $"Sub-process failed at node {subContext.CurrentNodeId}"
                };
            }

            // Si le sous-processus est en attente, sauvegarder son état
            if (subContext.Status == ProcessStatus.WaitingInteraction ||
                subContext.Status == ProcessStatus.WaitingDate ||
                subContext.Status == ProcessStatus.WaitingSignal)
            {
                // Sauvegarder l'ID du sous-processus dans le contexte parent
                context.Data[$"SUB_PROCESS_{Id}"] = subProcessId;

                return new NodeExecutionResult
                {
                    IsCompleted = true,
                    RequiresStop = true,
                    NextNodeId = NextNodeIds.FirstOrDefault()
                };
            }

            // Le sous-processus est complété
            // Récupérer les données de sortie du sous-processus
            foreach (var kvp in subContext.Data)
            {
                if (kvp.Key.StartsWith("OUTPUT_"))
                {
                    var keyName = kvp.Key.Replace("OUTPUT_", "SUB_OUTPUT_");
                    context.Data[keyName] = kvp.Value;
                }
            }

            // Nettoyer les données du sous-processus
            context.Data.Remove($"SUB_PROCESS_{Id}");

            // Supprimer le contexte du sous-processus de la base de données
            if (repository != null && !string.IsNullOrEmpty(existingSubProcessId))
            {
                await repository.DeleteProcessContextAsync(existingSubProcessId);
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
