namespace MedMateAI.Application.IService;

public interface ISmsSender
{
    Task<bool> SendAsync(
        string phoneNumber,
        string messageContent,
        CancellationToken cancellationToken = default);
}
