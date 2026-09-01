using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.MedicalFacilities.Requests;
using MedMateAI.Application.DTOs.MedicalFacilities.Responses;

namespace MedMateAI.Application.IService;

public interface IMedicalFacilityService
{
    Task<PagedResponse<MedicalFacilityResponse>> ListMedicalFacilitiesAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicalFacilityResponse>> ListActiveMedicalFacilitiesAsync(
        Guid? departmentId = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<(IEnumerable<string> Errors, IReadOnlyList<MedicalFacilityNearbyResponse> Data)> ListNearbyMedicalFacilitiesAsync(
        double latitude,
        double longitude,
        double radiusKm,
        Guid? departmentId = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<(IEnumerable<string> Errors, IReadOnlyList<MedicalFacilityResponse> Data)> ListTopRatedMedicalFacilitiesAsync(
        Guid? departmentId = null,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<MedicalFacilityResponse?> GetMedicalFacilityByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> CreateMedicalFacilityAsync(
        CreateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> UpdateMedicalFacilityAsync(
        Guid id,
        UpdateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> UpdateMedicalFacilityStatusAsync(
        Guid id,
        UpdateMedicalFacilityStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteMedicalFacilityAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
