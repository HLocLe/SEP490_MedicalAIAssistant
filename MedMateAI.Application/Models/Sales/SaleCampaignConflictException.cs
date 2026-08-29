namespace MedMateAI.Application.Models.Sales;

public sealed class SaleCampaignConflictException : InvalidOperationException
{
    public SaleCampaignConflictException(string message)
        : base(message)
    {
    }
}
