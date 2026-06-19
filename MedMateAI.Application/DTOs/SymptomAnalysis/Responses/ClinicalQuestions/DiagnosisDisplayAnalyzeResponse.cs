using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;

public sealed class DiagnosisDisplayAnalyzeResponse
{
    public Guid SessionId { get; set; }

    public SymptomAnalysisSessionStatus Status { get; set; }

    public string? Model { get; set; }

    public IReadOnlyList<BayesianDiagnosisResponse> Diagnoses { get; set; } =
        Array.Empty<BayesianDiagnosisResponse>();
}
