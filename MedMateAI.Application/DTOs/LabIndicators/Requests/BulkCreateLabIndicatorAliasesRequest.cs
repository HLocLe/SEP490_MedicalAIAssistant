namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class BulkCreateLabIndicatorAliasesRequest
{
    public IList<CreateLabIndicatorAliasRequest> Aliases { get; set; } = new List<CreateLabIndicatorAliasRequest>();
}
