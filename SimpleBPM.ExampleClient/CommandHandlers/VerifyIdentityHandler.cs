using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.CommandHandlers;

public class VerifyIdentityHandler : ICommandHandler
{
    public string CommandName => "VerifyIdentity";

    public Task HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Verifying applicant identity documents");
        return Task.CompletedTask;
    }
}
