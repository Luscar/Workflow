namespace SimpleBPM;

public class ProcessEngine
{
    private static ICommandQueryExecutor? _commandQueryExecutor;
    private readonly ProcessDefinition _definition;
    private readonly Persistence.IProcessRepository? _repository;

    public ProcessEngine(ProcessDefinition definition, Persistence.IProcessRepository? repository = null)
    {
        _definition = definition;
        _repository = repository;
    }

    /// <summary>
    /// Repository utilisé pour la persistance. Accessible pour les sous-processus.
    /// </summary>
    internal Persistence.IProcessRepository? Repository => _repository;

    public static void ConfigureExecutor(ICommandQueryExecutor executor)
    {
        _commandQueryExecutor = executor;
    }

    internal static ICommandQueryExecutor? GetCommandQueryExecutor()
    {
        return _commandQueryExecutor;
    }

    public async Task<ProcessContext> ExecuteAsync(ProcessContext context)
    {
        context.LastExecutedAt = DateTime.UtcNow;

        if (_repository != null)
        {
            var existingContext = await _repository.GetProcessContextAsync(context.ProcessId);
            if (existingContext == null)
            {
                await _repository.SaveProcessContextAsync(context);
            }
        }

        var currentNodeId = context.CurrentNodeId ?? _definition.StartNodeId;

        while (!string.IsNullOrEmpty(currentNodeId))
        {
            var node = _definition.GetNode(currentNodeId);

            if (node == null)
            {
                context.Status = ProcessStatus.Failed;
                if (_repository != null)
                {
                    await _repository.UpdateProcessContextAsync(context);
                }
                return context;
            }

            // Créer l'entrée d'historique
            var historyEntry = new NodeExecutionHistory(node.Id, node.Name, node.Type);

            var result = await node.ExecuteAsync(context, _repository);

            // Compléter l'entrée d'historique
            historyEntry.Complete(result.IsCompleted, result.ErrorMessage, result.NextNodeId);
            context.ExecutionHistory.Add(historyEntry);

            if (!result.IsCompleted)
            {
                context.Status = ProcessStatus.Failed;
                if (_repository != null)
                {
                    await _repository.UpdateProcessContextAsync(context);
                }
                return context;
            }

            if (result.RequiresStop)
            {
                context.CurrentNodeId = result.NextNodeId;
                if (_repository != null)
                {
                    await _repository.UpdateProcessContextAsync(context);
                }
                return context;
            }

            currentNodeId = result.NextNodeId;
        }

        context.Status = ProcessStatus.Completed;
        context.CurrentNodeId = null;
        context.CompletedAt = DateTime.UtcNow;
        if (_repository != null)
        {
            await _repository.UpdateProcessContextAsync(context);
        }
        return context;
    }

    public async Task<ProcessContext> ContinueAsync(ProcessContext context)
    {
        if (context.Status == ProcessStatus.Completed || context.Status == ProcessStatus.Failed)
        {
            return context;
        }

        if (string.IsNullOrEmpty(context.CurrentNodeId))
        {
            context.Status = ProcessStatus.Failed;
            if (_repository != null)
            {
                await _repository.UpdateProcessContextAsync(context);
            }
            return context;
        }

        context.Status = ProcessStatus.Running;

        var currentNode = _definition.GetNode(context.CurrentNodeId);
        if (currentNode == null || currentNode.NextNodeIds.Count == 0)
        {
            context.Status = ProcessStatus.Completed;
            context.CurrentNodeId = null;
            context.CompletedAt = DateTime.UtcNow;
            if (_repository != null)
            {
                await _repository.UpdateProcessContextAsync(context);
            }
            return context;
        }

        var nextNodeId = currentNode.NextNodeIds.FirstOrDefault();
        context.CurrentNodeId = nextNodeId;

        return await ExecuteAsync(context);
    }

    public async Task<ProcessContext> SignalAsync(ProcessContext context, string signalName)
    {
        if (context.Status != ProcessStatus.WaitingSignal)
        {
            return context;
        }

        if (context.Data.TryGetValue("WaitingForSignal", out var waitingSignal) &&
            waitingSignal?.ToString() == signalName)
        {
            return await ContinueAsync(context);
        }

        return context;
    }

    public async Task<ProcessContext?> LoadProcessAsync(string processId)
    {
        if (_repository == null)
        {
            throw new InvalidOperationException("No repository configured");
        }

        return await _repository.GetProcessContextAsync(processId);
    }
}
