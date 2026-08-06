namespace MedMateAI.Domain.Entities;

public sealed class MedicalDepartment : BaseEntity
{
    public string? DepartmentName { get; set; }

    public string? Description { get; set; }

    public string? ChapterCode { get; set; }

    public IcdChapter? IcdChapter { get; set; }

    public ICollection<FacilityDepartment> FacilityDepartments { get; set; } = new List<FacilityDepartment>();

    public ICollection<DepartmentRecommendation> DepartmentRecommendations { get; set; } = new List<DepartmentRecommendation>();

    public ICollection<ConsultationSession> ConsultationSessions { get; set; } = new List<ConsultationSession>();

    public ICollection<DepartmentConsultationQuestion> DepartmentConsultationQuestions { get; set; } =
        new List<DepartmentConsultationQuestion>();

    public ICollection<TreatmentJourney> TreatmentJourneys { get; set; } = new List<TreatmentJourney>();
}
