using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class ApproveLoanHandler : ICommandHandler
{
    public string CommandName => "ApproveLoan";

    public Task HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Loan APPROVED - generating approval letter");
        return Task.CompletedTask;
    }
}
