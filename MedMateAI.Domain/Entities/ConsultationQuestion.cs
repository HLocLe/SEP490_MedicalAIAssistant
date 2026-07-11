namespace MedMateAI.Domain.Entities;

public sealed class ConsultationQuestion : BaseEntity
{
    public Guid ConsultationSessionId { get; set; }

    public string? QuestionText { get; set; }

    public string? Category { get; set; }

    public int Priority { get; set; }

    public ConsultationSession ConsultationSession { get; set; } = null!;
}
