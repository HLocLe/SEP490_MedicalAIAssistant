namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class CreateLabIndicatorWithDetailsRequest
{
    public CreateLabIndicatorRequest Indicator { get; set; } = null!;

    public IList<CreateLabIndicatorAliasRequest> Aliases { get; set; } =
        new List<CreateLabIndicatorAliasRequest>();

    public IList<CreateLabIndicatorReferenceRangeRequest> ReferenceRanges { get; set; } =
        new List<CreateLabIndicatorReferenceRangeRequest>();

    public IList<CreateLabIndicatorAdviceCacheRequest> AdviceCaches { get; set; } =
        new List<CreateLabIndicatorAdviceCacheRequest>();
}
