using System.ComponentModel.DataAnnotations;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Requests;

public sealed class UpsertSubscriptionPlanQuotaRequest
{
    [Range(0, int.MaxValue)]
    public int LimitValue { get; set; }

    [EnumDataType(typeof(QuotaResetPeriod))]
    public QuotaResetPeriod ResetPeriod { get; set; }
        = QuotaResetPeriod.SubscriptionCycle;

    public bool IsActive { get; set; } = true;
}
