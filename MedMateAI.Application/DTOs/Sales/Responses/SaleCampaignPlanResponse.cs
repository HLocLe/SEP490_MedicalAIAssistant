namespace MedMateAI.Application.DTOs.Sales.Responses;

public sealed class SaleCampaignPlanResponse
{
    public Guid Id { get; set; }

    public Guid PlanId { get; set; }

    public string? PlanName { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? SalePrice { get; set; }

    public int BonusCredit { get; set; }

    public bool IsActive { get; set; }
}
