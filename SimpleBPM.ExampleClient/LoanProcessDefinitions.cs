using SimpleBPM;
using SimpleBPM.Definition;

namespace SimpleBPM.ExampleClient;

/// <summary>
/// Defines the loan approval workflow processes using the fluent ProcessBuilder API.
/// </summary>
public static class LoanProcessDefinitions
{
    /// <summary>
    /// Creates the main loan approval process definition.
    ///
    /// Workflow:
    ///   ValidateApplication -> Verification (subprocess) -> CheckCredit -> CreditDecision
    ///     - approved  -> CalculateTerms -> ManualReview (interactive) -> WaitDocumentSigning (signal) -> DisburseFunds
    ///     - rejected  -> RejectLoan
    /// </summary>
    public static ProcessDefinition CreateLoanApprovalProcess()
    {
        var verificationSubProcess = CreateVerificationSubProcess();

        return ProcessBuilder.Create("LoanApproval", "1.0")
            .Business("ValidateApplication", "Validate Loan Application")
                .WithParameter("RequiredDocuments", "ID,Income,Employment")
            .SubProcess("Verification", verificationSubProcess,
                inputMapping: new() { ["ApplicantId"] = "ApplicantId" },
                outputMapping: new() { ["VerificationPassed"] = "IsVerified" },
                displayName: "Applicant Verification")
            .Business("CheckCredit", "Check Credit Score")
            .Decision("CreditDecision", "Credit Decision", routes => routes
                .When("approved", "CalculateTerms")
                .When("rejected", "RejectLoan"))
            .Business("CalculateTerms", "Calculate Loan Terms")
                .Then("ManualReview").Break()
            .Business("RejectLoan", "Reject Loan Application").Break()
            .Interactive("ManualReview", "Underwriter Review")
            .WaitForSignal("WaitDocumentSigning", "Wait for Document Signing")
            .Business("DisburseFunds", "Disburse Loan Funds")
            .Build();
    }

    /// <summary>
    /// Creates a verification subprocess that checks identity, income, and employment.
    ///
    /// Workflow:
    ///   VerifyIdentity -> VerifyIncome -> VerifyEmployment
    /// </summary>
    public static ProcessDefinition CreateVerificationSubProcess()
    {
        return ProcessBuilder.Create("VerificationProcess", "1.0")
            .Business("VerifyIdentity", "Verify Identity Documents")
            .Business("VerifyIncome", "Verify Income Statements")
            .Business("VerifyEmployment", "Verify Employment Status")
            .Build();
    }
}
