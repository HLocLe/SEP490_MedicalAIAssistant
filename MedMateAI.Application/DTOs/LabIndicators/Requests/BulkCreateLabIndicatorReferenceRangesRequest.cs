namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class BulkCreateLabIndicatorReferenceRangesRequest
{
    public IList<CreateLabIndicatorReferenceRangeRequest> ReferenceRanges { get; set; } =
        new List<CreateLabIndicatorReferenceRangeRequest>();
}
