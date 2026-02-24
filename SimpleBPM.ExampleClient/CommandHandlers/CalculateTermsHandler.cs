using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class CalculateTermsHandler : ICommandHandler
{
    public string CommandName => "CalculateTerms";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Calculating loan terms and interest rate...");
        return Task.CompletedTask;
    }
}
