namespace MedMateAI.Application.DTOs.LabIndicators.Responses;

public sealed class LabIndicatorDetailResponse : LabIndicatorResponse
{
    public IReadOnlyList<LabIndicatorAliasResponse> Aliases { get; set; } =
        Array.Empty<LabIndicatorAliasResponse>();

    public IReadOnlyList<LabIndicatorReferenceRangeResponse> ReferenceRanges { get; set; } =
        Array.Empty<LabIndicatorReferenceRangeResponse>();

    public IReadOnlyList<LabIndicatorAdviceCacheResponse> AdviceCaches { get; set; } =
        Array.Empty<LabIndicatorAdviceCacheResponse>();
}
