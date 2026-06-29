using System.Text.Json.Serialization;

namespace MedMateAI.Application.DTOs.SymptomAnalysis.Responses.MedGemma;

internal sealed class MedGemmaDiagnosisJsonItem
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("disease_name")]
    public string DiseaseName { get; set; } = string.Empty;

    [JsonPropertyName("search_keyword")]
    public string SearchKeyword { get; set; } = string.Empty;

    [JsonPropertyName("p_A")]
    public double PA { get; set; }

    [JsonPropertyName("p_B_given_A")]
    public double PBGivenA { get; set; }

    [JsonPropertyName("clinical_reasoning")]
    public string ClinicalReasoning { get; set; } = string.Empty;
}
