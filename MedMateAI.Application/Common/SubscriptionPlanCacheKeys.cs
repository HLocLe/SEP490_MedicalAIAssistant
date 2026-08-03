namespace MedMateAI.Application.Common;

internal static class SubscriptionPlanCacheKeys
{
    public const string All = "subscription-plans:all";

    public const string Active = "subscription-plans:active";

    public static string ForPlan(Guid planId)
    {
        return $"subscription-plans:{planId}";
    }
}
