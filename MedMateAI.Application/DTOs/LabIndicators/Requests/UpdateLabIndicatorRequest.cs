namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class UpdateLabIndicatorRequest
{
    public string? Symbol { get; set; }

    public string? FullName { get; set; }

    public string? Unit { get; set; }

    public double? MinReference { get; set; }

    public double? MaxReference { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool? IsActive { get; set; }
}
