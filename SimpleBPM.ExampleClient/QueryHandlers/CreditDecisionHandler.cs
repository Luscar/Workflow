using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient.QueryHandlers;

public class CreditDecisionHandler : IQueryHandler
{
    public string QueryName => "CreditDecision";

    public Task<string> HandleAsync(long processId, string? aggregateId, Dictionary<string, object>? parameters = null)
    {
        Console.WriteLine($"  [{processId}] Evaluating credit decision...");
        var result = "approved"; // In reality, would check credit score from parameters/variables
        Console.WriteLine($"           Decision result: {result}");
        return Task.FromResult(result);
    }
}
