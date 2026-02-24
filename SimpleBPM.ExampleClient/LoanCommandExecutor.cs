using SimpleBPM.Abstractions;

namespace SimpleBPM.ExampleClient;

/// <summary>
/// Handles business command execution and decision evaluation for the loan approval workflow.
/// In a real application, these would call actual services (credit bureau API, database, etc.).
/// </summary>
public class LoanCommandExecutor : IBpmMediator
{
    public Task ExecuteCommandAsync(string commandName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        switch (commandName)
        {
            case "ValidateApplication":
                Console.WriteLine($"  [{processId}] Validating loan application for aggregate '{aggregateId}'");
                if (parameters != null)
                {
                    foreach (var p in parameters)
                        Console.WriteLine($"           Parameter: {p.Key} = {p.Value}");
                }
                break;

            case "CheckCredit":
                Console.WriteLine($"  [{processId}] Running credit check...");
                Console.WriteLine($"           Credit score retrieved: 720");
                break;

            case "CalculateTerms":
                Console.WriteLine($"  [{processId}] Calculating loan terms and interest rate...");
                break;

            case "ApproveLoan":
                Console.WriteLine($"  [{processId}] Loan APPROVED - generating approval letter");
                break;

            case "RejectLoan":
                Console.WriteLine($"  [{processId}] Loan REJECTED - generating rejection notice");
                break;

            case "DisburseFunds":
                Console.WriteLine($"  [{processId}] Disbursing funds to borrower account");
                break;

            case "VerifyIdentity":
                Console.WriteLine($"  [{processId}] Verifying applicant identity documents");
                break;

            case "VerifyIncome":
                Console.WriteLine($"  [{processId}] Verifying applicant income statements");
                break;

            case "VerifyEmployment":
                Console.WriteLine($"  [{processId}] Verifying applicant employment status");
                break;

            default:
                Console.WriteLine($"  [{processId}] Executing command: {commandName}");
                break;
        }

        return Task.CompletedTask;
    }

    public Task<string> EvaluateDecisionAsync(string decisionName, long processId, long? aggregateId,
        Dictionary<string, object>? parameters = null)
    {
        switch (decisionName)
        {
            case "CreditDecision":
                // Simulate a credit decision based on a threshold
                Console.WriteLine($"  [{processId}] Evaluating credit decision...");
                var result = "approved"; // In reality, would check credit score from parameters/variables
                Console.WriteLine($"           Decision result: {result}");
                return Task.FromResult(result);

            default:
                Console.WriteLine($"  [{processId}] Evaluating decision: {decisionName} -> approved");
                return Task.FromResult("approved");
        }
    }
}
