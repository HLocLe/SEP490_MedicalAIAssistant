namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanTemplateNutrientTarget : BaseEntity
{
    public Guid RecoveryPlanTemplatePhaseId { get; set; }
    public string NutrientName { get; set; } = string.Empty;
    public decimal AmountPerDay { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public RecoveryPlanTemplatePhase RecoveryPlanTemplatePhase { get; set; } = null!;
    public ICollection<RecoveryPlanTemplateFoodSource> FoodSources { get; set; } =
        new List<RecoveryPlanTemplateFoodSource>();
}
