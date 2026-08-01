namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class BulkCreateLabIndicatorAdviceCachesRequest
{
    public IList<CreateLabIndicatorAdviceCacheRequest> AdviceCaches { get; set; } =
        new List<CreateLabIndicatorAdviceCacheRequest>();
}
