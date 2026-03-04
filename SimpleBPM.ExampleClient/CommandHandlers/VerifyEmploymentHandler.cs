using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class VerifyEmploymentHandler : IBpmCommandHandler
{
    public string CommandName => "VerifyEmployment";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Verifying applicant employment status");
        return Task.CompletedTask;
    }
}
