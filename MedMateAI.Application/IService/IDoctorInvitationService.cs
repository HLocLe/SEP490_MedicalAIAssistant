using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.DoctorInvitations.Requests;
using MedMateAI.Application.DTOs.DoctorInvitations.Responses;

namespace MedMateAI.Application.IService;

public interface IDoctorInvitationService
{
    Task<DoctorInvitationResponse> CreateInvitationAsync(
        CreateDoctorInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<ValidateDoctorInvitationResponse> ValidateInvitationAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<RegisterDoctorByInvitationResponse> RegisterDoctorAsync(
        RegisterDoctorByInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<DoctorInvitationResponse?> RevokeInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, PagedResponse<DoctorInvitationResponse>? Data)>
        GetAdminInvitationsAsync(
            int pageNumber,
            int pageSize,
            string? status,
            string? search,
            CancellationToken cancellationToken = default);
}
