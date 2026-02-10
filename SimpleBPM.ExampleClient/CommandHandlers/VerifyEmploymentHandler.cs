using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class VerifyEmploymentHandler : ICommandHandler
{
    public string CommandName => "VerifyEmployment";

    public Task HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Verifying applicant employment status");
        return Task.CompletedTask;
    }
}
