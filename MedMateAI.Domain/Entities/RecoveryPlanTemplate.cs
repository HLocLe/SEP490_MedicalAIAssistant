using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanTemplate : BaseEntity
{
    public Guid DoctorId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Summary { get; set; }
    public string? RecheckInstruction { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public ICollection<RecoveryPlanTemplatePhase> Phases { get; set; } =
        new List<RecoveryPlanTemplatePhase>();
}
