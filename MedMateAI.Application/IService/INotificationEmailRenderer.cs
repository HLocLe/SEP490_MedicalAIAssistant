using MedMateAI.Application.Models.Notifications;

namespace MedMateAI.Application.IService;

public interface INotificationEmailRenderer
{
    NotificationEmailContent RenderRecoveryPlanReady();
    NotificationEmailContent RenderRecoveryPlanCompleted();
    NotificationEmailContent RenderRecoveryPlanCancelled(
        string? planName,
        string cancellationReasonCode,
        string? cancellationReason);
    NotificationEmailContent RenderMedicationReminder(
        string medicineName,
        string? dosageInstruction);
    NotificationEmailContent RenderSaleCampaignAnnouncement(
        SaleCampaignAnnouncementContext context,
        SaleCampaignNotificationContent content);
}
