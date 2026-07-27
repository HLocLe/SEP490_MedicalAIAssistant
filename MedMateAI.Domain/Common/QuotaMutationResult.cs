namespace MedMateAI.Domain.Common;

public sealed record QuotaMutationResult(
    Guid UsageId,
    Guid UserSubscriptionId,
    Guid QuotaId,
    int LimitValue,
    int UsedCountBefore,
    int UsedCountAfter,
    int ReservedCountBefore,
    int ReservedCountAfter);
