namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class CreateLabIndicatorRequest
{
    public string Symbol { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? Unit { get; set; }

    public double? MinReference { get; set; }

    public double? MaxReference { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}
