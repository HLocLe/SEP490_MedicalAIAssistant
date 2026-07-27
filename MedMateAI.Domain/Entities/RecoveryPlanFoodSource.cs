namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanFoodSource : BaseEntity
{
    public Guid RecoveryPlanNutrientTargetId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? SuggestedServing { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
    public RecoveryPlanNutrientTarget RecoveryPlanNutrientTarget { get; set; } = null!;
}
