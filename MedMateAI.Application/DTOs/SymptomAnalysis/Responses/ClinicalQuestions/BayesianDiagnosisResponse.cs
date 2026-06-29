namespace MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;

public sealed class BayesianDiagnosisResponse
{
    public int Rank { get; set; }

    public string DiseaseName { get; set; } = string.Empty;

    public string Icd10Code { get; set; } = string.Empty;

    public string SearchKeyword { get; set; } = string.Empty;

    public double PA { get; set; }

    public double PBGivenA { get; set; }

    public double PAGivenB { get; set; }

    public string ClinicalReasoning { get; set; } = string.Empty;
}
