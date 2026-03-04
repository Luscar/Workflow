using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class ValidateApplicationHandler : IBpmCommandHandler
{
    public string CommandName => "ValidateApplication";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Validating loan application for aggregate '{aggregateId}'");
        if (parameters != null)
        {
            foreach (var p in parameters)
                Console.WriteLine($"           Parameter: {p.Key} = {p.Value}");
        }

        return Task.CompletedTask;
    }
}
