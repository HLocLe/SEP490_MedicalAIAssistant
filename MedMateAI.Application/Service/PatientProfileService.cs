using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.PatientProfiles.Requests;
using MedMateAI.Application.DTOs.PatientProfiles.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Application.Service;

public sealed class PatientProfileService : IPatientProfileService
{
    private readonly IUserService _userService;
    private readonly IGenericRepository<PatientProfile> _patientProfiles;
    private readonly IGenericRepository<PatientChronicDisease> _chronicDiseases;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _uow;

    public PatientProfileService(
        IUserService userService,
        IGenericRepository<PatientProfile> patientProfiles,
        IGenericRepository<PatientChronicDisease> chronicDiseases,
        IMapper mapper,
        IUnitOfWork uow)
    {
        _userService = userService;
        _patientProfiles = patientProfiles;
        _chronicDiseases = chronicDiseases;
        _mapper = mapper;
        _uow = uow;
    }

   public async Task<(bool Succeeded, IEnumerable<string> Errors)> DeleteMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var current = await _userService.GetCurrentUserAsync(cancellationToken);
        if (current is null)
        {
            return (false, new[] { "Unauthorized." });
        }

        var entity = await _patientProfiles.FirstOrDefaultAsync(
            p => p.UserId == current.Id && !p.IsDeleted,
            asNoTracking: false,
            cancellationToken);

        if (entity is null)
        {
            return (false, new[] { "Patient profile not found." });
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await SoftDeleteChronicDiseasesAsync(entity.Id, cancellationToken);

        _patientProfiles.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }

    public async Task<PagedResponse<PatientProfileResponse>> ListPatientProfilesAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _patientProfiles.GetPagedAsync(
            pageNumber,
            pageSize,
            p => !p.IsDeleted,
            q => q.OrderByDescending(p => p.CreatedAt),
            cancellationToken: cancellationToken);

        var chronicDiseasesByProfileId = await LoadChronicDiseasesByProfileIdsAsync(
            paged.Items.Select(profile => profile.Id).ToList(),
            cancellationToken);

        return new PagedResponse<PatientProfileResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items
                .Select(profile => MapProfileResponse(profile, chronicDiseasesByProfileId))
                .ToList(),
        };
    }

    public async Task<(bool NotFound, PatientProfileResponse? Data)> GetPatientProfileByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _patientProfiles.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (true, null);
        }

        var chronicDiseasesByProfileId = await LoadChronicDiseasesByProfileIdsAsync(
            new[] { entity.Id },
            cancellationToken);

        return (false, MapProfileResponse(entity, chronicDiseasesByProfileId));
    }

    public async Task<(bool NotFound, PatientProfileResponse? Data)> GetPatientProfileByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (true, null);
        }

        var entity = await _patientProfiles.FirstOrDefaultAsync(
            p => p.UserId == userId && !p.IsDeleted,
            cancellationToken: cancellationToken);

        if (entity is null)
        {
            return (true, null);
        }

        var chronicDiseasesByProfileId = await LoadChronicDiseasesByProfileIdsAsync(
            new[] { entity.Id },
            cancellationToken);

        var response = MapProfileResponse(entity, chronicDiseasesByProfileId);
        var user = await _userService.GetUserByIdAsync(userId, cancellationToken);
        response.IsProfileCompleted = user?.IsProfileCompleted ?? false;

        return (false, response);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, PatientProfileResponse? Data)> CreatePatientProfileAsync(
        CreatePatientProfileRequest request,
        CancellationToken cancellationToken = default)
    {
       
        if (request.UserId==Guid.Empty)
        {
            return (false, new[] { "userid is required." }, null);
        }

        var validationErrors = ValidateChronicDiseaseCreateItems(request.ChronicDiseases);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var duplicate = await _patientProfiles.FirstOrDefaultAsync(
            p => p.UserId == request.UserId && !p.IsDeleted,
            asNoTracking: true,
            cancellationToken);
        
        if (duplicate is not null)
        {
            return (false, new[] { "A patient profile already exists for this user." }, null);
        }

        var entity = new PatientProfile
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            BloodType = string.IsNullOrWhiteSpace(request.BloodType) ? null : request.BloodType.Trim(),
            Height = request.Height,
            Weight = request.Weight,
            AllergyNote = string.IsNullOrWhiteSpace(request.AllergyNote) ? null : request.AllergyNote.Trim(),
        };
        _patientProfiles.Add(entity);

        AddChronicDiseases(entity.Id, request.ChronicDiseases);
        
        await _uow.SaveChangesAsync(cancellationToken);

        var mark = await _userService.MarkPatientProfileCompletedAsync(entity.UserId, cancellationToken);
        var chronicDiseasesByProfileId = await LoadChronicDiseasesByProfileIdsAsync(
            new[] { entity.Id },
            cancellationToken);
        var dto = MapProfileResponse(entity, chronicDiseasesByProfileId);
        var user = await _userService.GetUserByIdAsync(entity.UserId, cancellationToken);
        dto.IsProfileCompleted = user?.IsProfileCompleted ?? true;
       
        if (!mark.Succeeded)
        {
            return (false, mark.Errors, dto);
        }


        return (true, Array.Empty<string>(), dto);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, PatientProfileResponse? Data)> UpdatePatientProfileAsync(
        Guid id,
        UpdatePatientProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid patient profile id." }, null);
        }

        var validationErrors = ValidateChronicDiseaseUpdateItems(request.ChronicDiseases);
        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        var entity = await _patientProfiles.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Patient profile not found." }, null);
        }

        if (request.BloodType is not null)
        {
            entity.BloodType = string.IsNullOrWhiteSpace(request.BloodType) ? null : request.BloodType.Trim();
        }

        if (request.Height.HasValue)
        {
            entity.Height = request.Height;
        }

        if (request.Weight.HasValue)
        {
            entity.Weight = request.Weight;
        }

        if (request.AllergyNote is not null)
        {
            entity.AllergyNote = string.IsNullOrWhiteSpace(request.AllergyNote) ? null : request.AllergyNote.Trim();
        }

        if (request.ChronicDiseases is not null)
        {
            var syncErrors = await SyncChronicDiseasesAsync(entity.Id, request.ChronicDiseases, cancellationToken);
            if (syncErrors.Count > 0)
            {
                return (false, false, syncErrors, null);
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
        _patientProfiles.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        var chronicDiseasesByProfileId = await LoadChronicDiseasesByProfileIdsAsync(
            new[] { entity.Id },
            cancellationToken);

        return (true, false, Array.Empty<string>(), MapProfileResponse(entity, chronicDiseasesByProfileId));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeletePatientProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid patient profile id." });
        }

        var entity = await _patientProfiles.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Patient profile not found." });
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await SoftDeleteChronicDiseasesAsync(entity.Id, cancellationToken);

        _patientProfiles.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private PatientProfileResponse MapProfileResponse(
        PatientProfile profile,
        IReadOnlyDictionary<Guid, List<PatientChronicDisease>> chronicDiseasesByProfileId)
    {
        chronicDiseasesByProfileId.TryGetValue(profile.Id, out var chronicDiseases);
        profile.ChronicDiseases = chronicDiseases ?? new List<PatientChronicDisease>();

        return _mapper.Map<PatientProfileResponse>(profile);
    }

    private async Task<IReadOnlyDictionary<Guid, List<PatientChronicDisease>>> LoadChronicDiseasesByProfileIdsAsync(
        IReadOnlyList<Guid> profileIds,
        CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return new Dictionary<Guid, List<PatientChronicDisease>>();
        }

        var paged = await _chronicDiseases.GetPagedAsync(
            1,
            1000,
            disease => !disease.IsDeleted && profileIds.Contains(disease.PatientProfileId),
            query => query.OrderBy(disease => disease.CreatedAt),
            cancellationToken: cancellationToken);

        return paged.Items
            .GroupBy(disease => disease.PatientProfileId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private void AddChronicDiseases(
        Guid patientProfileId,
        IReadOnlyList<PatientChronicDiseaseItemCreateRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            _chronicDiseases.Add(new PatientChronicDisease
            {
                Id = Guid.NewGuid(),
                PatientProfileId = patientProfileId,
                DiseaseName = item.DiseaseName.Trim(),
                From = item.From,
                To = item.To,
                Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    private async Task<IReadOnlyList<string>> SyncChronicDiseasesAsync(
        Guid patientProfileId,
        IReadOnlyList<PatientChronicDiseaseItemUpdateRequest> items,
        CancellationToken cancellationToken)
    {
        var existing = await _chronicDiseases.GetPagedAsync(
            1,
            1000,
            disease => !disease.IsDeleted && disease.PatientProfileId == patientProfileId,
            asNoTracking: false,
            cancellationToken: cancellationToken);

        var requestedIds = items
            .Where(item => item.Id.HasValue && item.Id.Value != Guid.Empty)
            .Select(item => item.Id!.Value)
            .ToHashSet();

        foreach (var existingDisease in existing.Items)
        {
            if (!requestedIds.Contains(existingDisease.Id))
            {
                existingDisease.IsDeleted = true;
                existingDisease.DeletedAt = DateTime.UtcNow;
                existingDisease.UpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var item in items)
        {
            if (item.Id.HasValue && item.Id.Value != Guid.Empty)
            {
                var existingDisease = existing.Items.FirstOrDefault(disease => disease.Id == item.Id.Value);
                if (existingDisease is null)
                {
                    return new[] { $"Chronic disease '{item.Id.Value}' was not found for this profile." };
                }

                existingDisease.DiseaseName = item.DiseaseName.Trim();
                existingDisease.From = item.From;
                existingDisease.To = item.To;
                existingDisease.Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();
                existingDisease.UpdatedAt = DateTime.UtcNow;
                continue;
            }

            _chronicDiseases.Add(new PatientChronicDisease
            {
                Id = Guid.NewGuid(),
                PatientProfileId = patientProfileId,
                DiseaseName = item.DiseaseName.Trim(),
                From = item.From,
                To = item.To,
                Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
                CreatedAt = DateTime.UtcNow,
            });
        }

        return Array.Empty<string>();
    }

    private async Task SoftDeleteChronicDiseasesAsync(
        Guid patientProfileId,
        CancellationToken cancellationToken)
    {
        var existing = await _chronicDiseases.GetPagedAsync(
            1,
            1000,
            disease => !disease.IsDeleted && disease.PatientProfileId == patientProfileId,
            asNoTracking: false,
            cancellationToken: cancellationToken);

        foreach (var disease in existing.Items)
        {
            disease.IsDeleted = true;
            disease.DeletedAt = DateTime.UtcNow;
            disease.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static IReadOnlyList<string> ValidateChronicDiseaseCreateItems(
        IReadOnlyList<PatientChronicDiseaseItemCreateRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            ValidateChronicDiseaseFields(item.DiseaseName, item.From, item.To, index, errors);
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateChronicDiseaseUpdateItems(
        IReadOnlyList<PatientChronicDiseaseItemUpdateRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            ValidateChronicDiseaseFields(item.DiseaseName, item.From, item.To, index, errors);
        }

        return errors;
    }

    private static void ValidateChronicDiseaseFields(
        string diseaseName,
        DateOnly? from,
        DateOnly? to,
        int index,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(diseaseName))
        {
            errors.Add($"Chronic disease at index {index}: disease name is required.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            errors.Add($"Chronic disease at index {index}: from date must be earlier than or equal to to date.");
        }
    }
}
