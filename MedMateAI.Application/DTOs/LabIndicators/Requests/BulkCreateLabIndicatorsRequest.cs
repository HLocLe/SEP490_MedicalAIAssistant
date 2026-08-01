namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class BulkCreateLabIndicatorsRequest
{
    public IList<CreateLabIndicatorRequest> Indicators { get; set; } = new List<CreateLabIndicatorRequest>();
}
