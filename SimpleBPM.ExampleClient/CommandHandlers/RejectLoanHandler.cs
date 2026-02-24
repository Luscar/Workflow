using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class RejectLoanHandler : ICommandHandler
{
    public string CommandName => "RejectLoan";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Loan REJECTED - generating rejection notice");
        return Task.CompletedTask;
    }
}
