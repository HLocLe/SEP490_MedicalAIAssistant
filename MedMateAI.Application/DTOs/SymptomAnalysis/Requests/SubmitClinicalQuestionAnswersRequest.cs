namespace MedMateAI.Application.DTOs.SymptomAnalysis.Requests;

public sealed class SubmitClinicalQuestionAnswersRequest
{
    public Guid SessionId { get; set; }

    public List<ClinicalQuestionAnswerItem> Answers { get; set; } = new();
}
