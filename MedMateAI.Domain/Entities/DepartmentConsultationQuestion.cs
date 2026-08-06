using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class DepartmentConsultationQuestion : BaseEntity
{
    public Guid DepartmentId { get; set; }

    public ConsultationQuestionCategory Category { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public MedicalDepartment Department { get; set; } = null!;
}
