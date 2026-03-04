using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class VerifyIncomeHandler : IBpmCommandHandler
{
    public string CommandName => "VerifyIncome";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Verifying applicant income statements");
        return Task.CompletedTask;
    }
}
