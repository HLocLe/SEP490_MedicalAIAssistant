using MedMateAI.Application.Models.Notifications;

namespace MedMateAI.Application.IService;

public interface IPushNotificationGateway
{
    Task<PushSendResult> SendAsync(
        PushNotificationMessage message,
        CancellationToken cancellationToken = default);

    Task<PushReceiptBatchResult> GetReceiptsAsync(
        IReadOnlyCollection<string> providerMessageIds,
        CancellationToken cancellationToken = default);
}
