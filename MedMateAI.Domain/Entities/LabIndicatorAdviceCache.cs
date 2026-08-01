using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class LabIndicatorAdviceCache : BaseEntity
{
    public Guid IndicatorId { get; set; }

    public LabResultStatus Status { get; set; }

    public string? DisplayTitle { get; set; }

    public string? Summary { get; set; }

    public string? PossibleCauses { get; set; }

    public string? LifestyleAdvice { get; set; }

    public string? NutritionalAdvice { get; set; }

    public string? UrgencyLevel { get; set; }

    public LabAdviceSeverityLevel SeverityLevel { get; set; } = LabAdviceSeverityLevel.Info;

    public string? WarningSigns { get; set; }

    public string? FollowUpSuggestion { get; set; }

    public string? DoctorQuestions { get; set; }

    public LabIndicatorMaster Indicator { get; set; } = null!;

    public ICollection<LabTestResultDetail> LabTestResultDetails { get; set; } = new List<LabTestResultDetail>();
}
