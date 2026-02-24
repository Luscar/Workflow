using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class DisburseFundsHandler : ICommandHandler
{
    public string CommandName => "DisburseFunds";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Disbursing funds to borrower account");
        return Task.CompletedTask;
    }
}
