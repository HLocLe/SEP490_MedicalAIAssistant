namespace MedMateAI.Application.Models.ServiceCredits;

internal static class ServiceCreditErrorCodeExtensions
{
    public static string ToStableCode(this ServiceCreditErrorCode error) =>
        error switch
        {
            ServiceCreditErrorCode.NoCreditPackage => "NO_CREDIT_PACKAGE",
            ServiceCreditErrorCode.ServiceCreditExhausted => "SERVICE_CREDIT_EXHAUSTED",
            ServiceCreditErrorCode.ServiceCreditNotConfigured => "SERVICE_CREDIT_NOT_CONFIGURED",
            _ => "QUOTA_MUTATION_FAILED"
        };
}
