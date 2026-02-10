using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class VerifyIncomeHandler : ICommandHandler
{
    public string CommandName => "VerifyIncome";

    public Task HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Verifying applicant income statements");
        return Task.CompletedTask;
    }
}
