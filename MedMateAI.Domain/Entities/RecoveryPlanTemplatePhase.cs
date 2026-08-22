namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanTemplatePhase : BaseEntity
{
    public Guid RecoveryPlanTemplateId { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    public decimal? SleepAndRestHoursPerDay { get; set; }
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public RecoveryPlanTemplate RecoveryPlanTemplate { get; set; } = null!;
    public ICollection<RecoveryPlanTemplateNutrientTarget> NutrientTargets { get; set; } =
        new List<RecoveryPlanTemplateNutrientTarget>();
}
