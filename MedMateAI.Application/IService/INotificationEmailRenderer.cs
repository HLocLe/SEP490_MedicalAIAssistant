using MedMateAI.Application.Models.Notifications;

namespace MedMateAI.Application.IService;

public interface INotificationEmailRenderer
{
    NotificationEmailContent RenderRecoveryPlanReady();
    NotificationEmailContent RenderRecoveryPlanCompleted();
    NotificationEmailContent RenderMedicationReminder(
        string medicineName,
        string? dosageInstruction);
}
