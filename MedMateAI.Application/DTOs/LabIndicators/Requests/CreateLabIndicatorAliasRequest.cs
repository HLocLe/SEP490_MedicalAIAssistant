namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class CreateLabIndicatorAliasRequest
{
    public string AliasText { get; set; } = string.Empty;

    public string? Language { get; set; }

    public bool IsPrimary { get; set; }
}
