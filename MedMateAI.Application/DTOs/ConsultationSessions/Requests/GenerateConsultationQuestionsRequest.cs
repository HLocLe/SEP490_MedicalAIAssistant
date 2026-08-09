namespace MedMateAI.Application.DTOs.ConsultationSessions.Requests;

public sealed class GenerateConsultationQuestionsRequest
{
    public Guid DepartmentId { get; set; }

    public Guid? FacilityId { get; set; }

    public DateTime? AppointmentTime { get; set; }

    public string Symptoms { get; set; } = string.Empty;
}
