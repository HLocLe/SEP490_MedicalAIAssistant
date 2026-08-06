namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanPhase : BaseEntity
{
    public Guid RecoveryPlanId { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    public decimal? SleepAndRestHoursPerDay { get; set; }
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public RecoveryPlan RecoveryPlan { get; set; } = null!;
    public ICollection<RecoveryPlanNutrientTarget> NutrientTargets { get; set; } = new List<RecoveryPlanNutrientTarget>();
}
