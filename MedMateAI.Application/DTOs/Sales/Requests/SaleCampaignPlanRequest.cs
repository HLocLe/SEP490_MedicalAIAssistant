namespace MedMateAI.Application.DTOs.Sales.Requests;

public sealed class SaleCampaignPlanRequest
{
    public Guid PlanId { get; set; }

    public decimal? SalePrice { get; set; }

    public int BonusCredit { get; set; }

    public bool IsActive { get; set; } = true;
}
