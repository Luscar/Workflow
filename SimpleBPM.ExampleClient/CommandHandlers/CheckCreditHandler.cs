using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class CheckCreditHandler : ICommandHandler
{
    public string CommandName => "CheckCredit";

    public Task HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Running credit check...");
        Console.WriteLine($"           Credit score retrieved: 720");
        return Task.CompletedTask;
    }
}
