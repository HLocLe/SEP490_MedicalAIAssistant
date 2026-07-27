namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanNutrientTarget : BaseEntity
{
    public Guid RecoveryPlanPhaseId { get; set; }
    public string NutrientName { get; set; } = string.Empty;
    public decimal AmountPerDay { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public RecoveryPlanPhase RecoveryPlanPhase { get; set; } = null!;
    public ICollection<RecoveryPlanFoodSource> FoodSources { get; set; } = new List<RecoveryPlanFoodSource>();
}
