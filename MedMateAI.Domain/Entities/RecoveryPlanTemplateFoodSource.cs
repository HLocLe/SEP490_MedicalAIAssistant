namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanTemplateFoodSource : BaseEntity
{
    public Guid RecoveryPlanTemplateNutrientTargetId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? SuggestedServing { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
    public RecoveryPlanTemplateNutrientTarget RecoveryPlanTemplateNutrientTarget { get; set; } =
        null!;
}
