using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class VerifyIdentityHandler : IBpmCommandHandler
{
    public string CommandName => "VerifyIdentity";

    public Task HandleAsync(long processId, long? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Verifying applicant identity documents");
        return Task.CompletedTask;
    }
}
