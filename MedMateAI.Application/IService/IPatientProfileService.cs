using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.PatientProfiles.Requests;
using MedMateAI.Application.DTOs.PatientProfiles.Responses;

namespace MedMateAI.Application.IService;

public interface IPatientProfileService
{
    Task<(bool Succeeded, IEnumerable<string> Errors)> DeleteMyProfileAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResponse<PatientProfileResponse>> ListPatientProfilesAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool NotFound, PatientProfileResponse? Data)> GetPatientProfileByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool NotFound, PatientProfileResponse? Data)> GetPatientProfileByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, PatientProfileResponse? Data)> CreatePatientProfileAsync(
        CreatePatientProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, PatientProfileResponse? Data)> UpdatePatientProfileAsync(
        Guid id,
        UpdatePatientProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeletePatientProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
