using MedMateAI.Application.DTOs.PushDevices;
using MedMateAI.Application.Models.PushDevices;

namespace MedMateAI.Application.IService;

public interface IUserPushDeviceService
{
    Task<PushDeviceOperationResult<PushDeviceResponse>> RegisterAsync(
        Guid userId,
        RegisterPushDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<PushDeviceOperationResult<bool>> DeactivateAsync(
        Guid userId,
        string installationId,
        CancellationToken cancellationToken = default);
}
