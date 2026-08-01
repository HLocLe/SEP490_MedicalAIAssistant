using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabIndicators.Responses;

public sealed class LabIndicatorAliasResponse
{
    public Guid AliasId { get; set; }

    public Guid IndicatorId { get; set; }

    public string AliasText { get; set; } = string.Empty;

    public string? Language { get; set; }

    public bool IsPrimary { get; set; }
}
