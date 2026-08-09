namespace MedMateAI.Application.IService;

public interface ISmsSender
{
    Task<bool> SendAsync(
        string phoneNumber,
        string messageContent,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default);
}
