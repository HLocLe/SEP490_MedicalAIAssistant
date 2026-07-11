using System.Text.Json.Serialization;

namespace MedMateAI.Application.DTOs.ConsultationSessions.Responses;

public sealed class ConsultationDoctorQuestionItemResponse
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;
}
