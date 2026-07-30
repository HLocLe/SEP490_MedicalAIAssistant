using MedMateAI.Application.IService;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanRealtimeAccessService : IRecoveryPlanRealtimeAccessService
{
    private readonly IUnitOfWork _unitOfWork;

    public RecoveryPlanRealtimeAccessService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RecoveryPlanRealtimeDoctorAccess?> GetDoctorAccessAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken)
    {
        var access = await _unitOfWork.RecoveryPlanRequests.GetRealtimeDoctorAccessAsync(
            doctorUserId,
            cancellationToken);

        if (access is null)
        {
            return null;
        }

        return new RecoveryPlanRealtimeDoctorAccess(
            access.DoctorId,
            access.IsActive,
            access.IsAcceptingRecoveryPlanRequests,
            access.IsAccountValid);
    }
}
