using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.PatientProfiles.Requests;
using MedMateAI.Application.DTOs.PatientProfiles.Responses;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Application.Service;

public sealed class PatientProfileService : IPatientProfileService
{
    private const int MaxChronicDiseaseItems = 100;
    private const string AdminRole = "Admin";
    private const string DeletedUserAccountMessage =
        "Tài khoản người dùng đã bị xóa. Không thể cập nhật hồ sơ.";

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
            return (false, new[] { "Người dùng chưa đăng nhập." });
        }

        var entity = await _patientProfiles.FirstOrDefaultAsync(
            p => p.UserId == current.Id && !p.IsDeleted,
            asNoTracking: false,
            cancellationToken);

        if (entity is null)
        {
            return (false, new[] { "Không tìm thấy hồ sơ bệnh nhân." });
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
        var caller = await ResolveCallerAsync(cancellationToken);
        if (caller.Current is null)
        {
            return (true, null);
        }

        var entity = await _patientProfiles.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (true, null);
        }

        if (!CanAccessResource(entity.UserId, caller))
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

        var caller = await ResolveCallerAsync(cancellationToken);
        if (caller.Current is null || !CanAccessResource(userId, caller))
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
        var caller = await ResolveCallerAsync(cancellationToken);
        if (caller.Current is null)
        {
            return (false, new[] { "Người dùng chưa đăng nhập." }, null);
        }

       
        var ownerUserId = caller.Current.Id;

        if (await IsOwnerAccountDeletedAsync(ownerUserId, cancellationToken))
        {
            return (false, new[] { DeletedUserAccountMessage }, null);
        }

        var profileFieldErrors = ValidateHeightAndWeight(request.Height, request.Weight);
        if (profileFieldErrors.Count > 0)
        {
            return (false, profileFieldErrors, null);
        }

        var validationErrors = ValidateChronicDiseaseCreateItems(request.ChronicDiseases);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var duplicate = await _patientProfiles.FirstOrDefaultAsync(
            p => p.UserId == ownerUserId && !p.IsDeleted,
            asNoTracking: true,
            cancellationToken);
        
        if (duplicate is not null)
        {
            return (false, new[] { "Người dùng này đã có hồ sơ bệnh nhân." }, null);
        }

        var softDeleted = await _patientProfiles.FirstOrDefaultAsync(
            p => p.UserId == ownerUserId && p.IsDeleted,
            asNoTracking: true,
            cancellationToken);

        if (softDeleted is not null)
        {
            return (false, new[]
            {
                "Hồ sơ bệnh nhân đã từng bị xóa. Không thể tạo mới do ràng buộc dữ liệu. Vui lòng liên hệ hỗ trợ để khôi phục hồ sơ.",
            }, null);
        }

        var entity = new PatientProfile
        {
            Id = Guid.NewGuid(),
            UserId = ownerUserId,
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
            return (false, false, new[] { "Id hồ sơ bệnh nhân không hợp lệ." }, null);
        }

        var caller = await ResolveCallerAsync(cancellationToken);
        if (caller.Current is null)
        {
            return (false, false, new[] { "Người dùng chưa đăng nhập." }, null);
        }

        var profileFieldErrors = ValidateHeightAndWeight(request.Height, request.Weight);
        if (profileFieldErrors.Count > 0)
        {
            return (false, false, profileFieldErrors, null);
        }

        var validationErrors = ValidateChronicDiseaseUpdateItems(request.ChronicDiseases);
        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        var entity = await _patientProfiles.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy hồ sơ bệnh nhân." }, null);
        }

        if (!CanAccessResource(entity.UserId, caller))
        {
            return (false, true, new[] { "Không tìm thấy hồ sơ bệnh nhân." }, null);
        }

        if (await IsOwnerAccountDeletedAsync(entity.UserId, cancellationToken))
        {
            return (false, false, new[] { DeletedUserAccountMessage }, null);
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
            return (false, false, new[] { "Id hồ sơ bệnh nhân không hợp lệ." });
        }

        var caller = await ResolveCallerAsync(cancellationToken);
        if (caller.Current is null)
        {
            return (false, false, new[] { "Người dùng chưa đăng nhập." });
        }

        var entity = await _patientProfiles.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy hồ sơ bệnh nhân." });
        }

        if (!CanAccessResource(entity.UserId, caller))
        {
            return (false, true, new[] { "Không tìm thấy hồ sơ bệnh nhân." });
        }

        if (await IsOwnerAccountDeletedAsync(entity.UserId, cancellationToken))
        {
            return (false, false, new[] { DeletedUserAccountMessage });
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await SoftDeleteChronicDiseasesAsync(entity.Id, cancellationToken);

        _patientProfiles.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private async Task<(ApplicationUserResponse? Current, bool IsAdmin)> ResolveCallerAsync(
        CancellationToken cancellationToken)
    {
        var current = await _userService.GetCurrentUserAsync(cancellationToken);
        if (current is null)
        {
            return (null, false);
        }

        var isAdmin = await _userService.IsInRoleAsync(current.Id, AdminRole, cancellationToken);
        return (current, isAdmin);
    }

    private static bool CanAccessResource(
        Guid resourceUserId,
        (ApplicationUserResponse? Current, bool IsAdmin) caller) =>
        caller.Current is not null
        && (caller.IsAdmin || caller.Current.Id == resourceUserId);

    
    private async Task<bool> IsOwnerAccountDeletedAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var owner = await _userService.GetUserByIdAsync(userId, cancellationToken);
        return owner is null;
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
                Note = NormalizeChronicDiseaseNote(item.Note),
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

        MarkRemovedChronicDiseases(existing.Items, GetRequestedChronicDiseaseIds(items));

        foreach (var item in items)
        {
            var syncError = ApplyChronicDiseaseSyncItem(patientProfileId, item, existing.Items);
            if (syncError is not null)
            {
                return new[] { syncError };
            }
        }

        return Array.Empty<string>();
    }

    private static HashSet<Guid> GetRequestedChronicDiseaseIds(
        IReadOnlyList<PatientChronicDiseaseItemUpdateRequest> items)
    {
        return items
            .Where(item => HasExistingChronicDiseaseId(item.Id))
            .Select(item => item.Id!.Value)
            .ToHashSet();
    }

    private static void MarkRemovedChronicDiseases(
        IReadOnlyList<PatientChronicDisease> existingDiseases,
        HashSet<Guid> requestedIds)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var existingDisease in existingDiseases.Where(disease => !requestedIds.Contains(disease.Id)))
        {
            existingDisease.IsDeleted = true;
            existingDisease.DeletedAt = utcNow;
            existingDisease.UpdatedAt = utcNow;
        }
    }

    private string? ApplyChronicDiseaseSyncItem(
        Guid patientProfileId,
        PatientChronicDiseaseItemUpdateRequest item,
        IReadOnlyList<PatientChronicDisease> existingDiseases)
    {
        if (!HasExistingChronicDiseaseId(item.Id))
        {
            AddChronicDiseaseFromUpdateRequest(patientProfileId, item);
            return null;
        }

        var existingDisease = existingDiseases.FirstOrDefault(disease => disease.Id == item.Id!.Value);
        if (existingDisease is null)
        {
            return $"Không tìm thấy bệnh mãn tính '{item.Id!.Value}' trong hồ sơ này.";
        }

        UpdateChronicDiseaseFromRequest(existingDisease, item);
        return null;
    }

    private void AddChronicDiseaseFromUpdateRequest(
        Guid patientProfileId,
        PatientChronicDiseaseItemUpdateRequest item)
    {
        _chronicDiseases.Add(new PatientChronicDisease
        {
            Id = Guid.NewGuid(),
            PatientProfileId = patientProfileId,
            DiseaseName = item.DiseaseName.Trim(),
            From = item.From,
            To = item.To,
            Note = NormalizeChronicDiseaseNote(item.Note),
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static void UpdateChronicDiseaseFromRequest(
        PatientChronicDisease existingDisease,
        PatientChronicDiseaseItemUpdateRequest item)
    {
        existingDisease.DiseaseName = item.DiseaseName.Trim();
        existingDisease.From = item.From;
        existingDisease.To = item.To;
        existingDisease.Note = NormalizeChronicDiseaseNote(item.Note);
        existingDisease.UpdatedAt = DateTime.UtcNow;
    }

    private static bool HasExistingChronicDiseaseId(Guid? id) =>
        id.HasValue && id.Value != Guid.Empty;

    private static string? NormalizeChronicDiseaseNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

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
        IReadOnlyList<PatientChronicDiseaseItemCreateRequest>? items) =>
        ValidateChronicDiseaseItems(items, item => (item.DiseaseName, item.From, item.To));

    private static IReadOnlyList<string> ValidateChronicDiseaseUpdateItems(
        IReadOnlyList<PatientChronicDiseaseItemUpdateRequest>? items) =>
        ValidateChronicDiseaseItems(items, item => (item.DiseaseName, item.From, item.To));

    private static IReadOnlyList<string> ValidateChronicDiseaseItems<T>(
        IReadOnlyList<T>? items,
        Func<T, (string DiseaseName, DateOnly? From, DateOnly? To)> fieldSelector)
    {
        if (items is null || items.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (items.Count > MaxChronicDiseaseItems)
        {
            return new[] { $"Chỉ được phép tối đa {MaxChronicDiseaseItems} bệnh mãn tính." };
        }

        var errors = new List<string>();

        for (var index = 0; index < MaxChronicDiseaseItems; index++)
        {
            if (index >= items.Count)
            {
                break;
            }

            var fields = fieldSelector(items[index]);
            ValidateChronicDiseaseFields(fields.DiseaseName, fields.From, fields.To, index, errors);
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
            errors.Add($"Bệnh mãn tính tại vị trí {index}: tên bệnh là bắt buộc.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            errors.Add($"Bệnh mãn tính tại vị trí {index}: ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        }
    }

    private static IReadOnlyList<string> ValidateHeightAndWeight(double? height, double? weight)
    {
        var errors = new List<string>();

        if (height.HasValue && height.Value < 0)
        {
            errors.Add("Chiều cao không được âm.");
        }

        if (weight.HasValue && weight.Value < 0)
        {
            errors.Add("Cân nặng không được âm.");
        }

        return errors;
    }
}
