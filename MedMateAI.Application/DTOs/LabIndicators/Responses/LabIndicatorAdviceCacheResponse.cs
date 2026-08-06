using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabIndicators.Responses;

public sealed class LabIndicatorAdviceCacheResponse
{
    public Guid CacheId { get; set; }

    public Guid IndicatorId { get; set; }

    public LabResultStatus Status { get; set; }

    public string? DisplayTitle { get; set; }

    public string? Summary { get; set; }

    public string? PossibleCauses { get; set; }

    public string? LifestyleAdvice { get; set; }

    public string? NutritionalAdvice { get; set; }

    public string? UrgencyLevel { get; set; }

    public LabAdviceSeverityLevel SeverityLevel { get; set; }

    public string? WarningSigns { get; set; }
}
