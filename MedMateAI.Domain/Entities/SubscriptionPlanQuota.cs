using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class SubscriptionPlanQuota : BaseEntity
{
    public Guid PlanId { get; set; }
    public Guid QuotaId { get; set; }
    public int LimitValue { get; set; }
    public QuotaResetPeriod ResetPeriod { get; set; }
    public bool IsActive { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public Quota Quota { get; set; } = null!;
}
