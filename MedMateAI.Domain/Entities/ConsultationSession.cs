using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class ConsultationSession : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public string? UserSymptoms { get; set; }

    public ConsultationSessionStatus Status { get; set; } = ConsultationSessionStatus.Processing;

    public MedicalDepartment Department { get; set; } = null!;

    public ICollection<ConsultationQuestion> ConsultationQuestions { get; set; } = new List<ConsultationQuestion>();

    public ICollection<AISystemConfig> AISystemConfigs { get; set; } = new List<AISystemConfig>();

    public ICollection<AIAnalysis> AIAnalyses { get; set; } = new List<AIAnalysis>();
}
