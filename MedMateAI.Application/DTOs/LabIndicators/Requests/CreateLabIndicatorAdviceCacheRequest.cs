using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class CreateLabIndicatorAdviceCacheRequest
{
    public LabResultStatus Status { get; set; }

    public string? DisplayTitle { get; set; }

    public string? Summary { get; set; }

    public string? PossibleCauses { get; set; }

    public string? LifestyleAdvice { get; set; }

    public string? NutritionalAdvice { get; set; }

    public string? UrgencyLevel { get; set; }

    public LabAdviceSeverityLevel SeverityLevel { get; set; } = LabAdviceSeverityLevel.Info;

    public string? WarningSigns { get; set; }
}
