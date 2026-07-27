using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanRequestEvent
{
    public Guid Id { get; set; }
    public Guid RecoveryPlanRequestId { get; set; }
    public RecoveryPlanRequestEventType EventType { get; set; }
    public RecoveryPlanRequestStatus? FromStatus { get; set; }
    public RecoveryPlanRequestStatus? ToStatus { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? ActorDoctorId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public RecoveryPlanRequest RecoveryPlanRequest { get; set; } = null!;
}
