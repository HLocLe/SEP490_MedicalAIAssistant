using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabIndicators.Requests;
using MedMateAI.Application.DTOs.LabIndicators.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class LabIndicatorService : ILabIndicatorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public LabIndicatorService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<LabIndicatorResponse>> ListLabIndicatorsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        var paged = await _unitOfWork.LabIndicators.GetPagedAsync(
            pageNumber,
            pageSize,
            indicator => !indicator.IsDeleted
                && (searchTerm == null
                    || indicator.Symbol.ToLower().Contains(searchTerm)
                    || (indicator.FullName != null && indicator.FullName.ToLower().Contains(searchTerm))
                    || (indicator.Category != null && indicator.Category.ToLower().Contains(searchTerm))),
            query => query.OrderBy(indicator => indicator.Symbol),
            cancellationToken: cancellationToken);

        return new PagedResponse<LabIndicatorResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(indicator => _mapper.Map<LabIndicatorResponse>(indicator)).ToList(),
        };
    }

    public async Task<LabIndicatorDetailResponse?> GetLabIndicatorByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var indicator = await _unitOfWork.LabIndicators.GetByIdWithDetailsAsync(id, cancellationToken);
        return indicator is null ? null : _mapper.Map<LabIndicatorDetailResponse>(indicator);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, LabIndicatorResponse? Data)> CreateLabIndicatorAsync(
        CreateLabIndicatorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body là bắt buộc" }, null);
        }

        var validationErrors = ValidateIndicatorFields(request.Symbol, request.MinReference, request.MaxReference);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var symbol = NormalizeSymbol(request.Symbol)!;

        if (await _unitOfWork.LabIndicators.SymbolExistsAsync(symbol, cancellationToken: cancellationToken))
        {
            return (false, new[] { "Ký hiệu chỉ số đã tồn tại" }, null);
        }

        var entity = MapToMasterEntity(request, symbol);
        _unitOfWork.LabIndicators.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), _mapper.Map<LabIndicatorResponse>(entity));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorResponse>? Data)> BulkCreateLabIndicatorsAsync(
        BulkCreateLabIndicatorsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Indicators is null || request.Indicators.Count == 0)
        {
            return (false, new[] { "At least one indicator is required." }, null);
        }

        var errors = new List<string>();
        var prepared = new List<(CreateLabIndicatorRequest Request, string Symbol)>();

        for (var index = 0; index < request.Indicators.Count; index++)
        {
            var item = request.Indicators[index];
            var fieldErrors = ValidateIndicatorFields(item.Symbol, item.MinReference, item.MaxReference);
            foreach (var fieldError in fieldErrors)
            {
                errors.Add($"Indicators[{index}]: {fieldError}");
            }

            if (fieldErrors.Count > 0)
            {
                continue;
            }

            prepared.Add((item, NormalizeSymbol(item.Symbol)!));
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        var duplicateSymbols = prepared
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSymbols.Count > 0)
        {
            return (false, duplicateSymbols.Select(symbol => $"Duplicate symbol in request: {symbol}"), null);
        }

        foreach (var symbol in prepared.Select(x => x.Symbol))
        {
            if (await _unitOfWork.LabIndicators.SymbolExistsAsync(symbol, cancellationToken: cancellationToken))
            {
                errors.Add($"Ký hiệu chỉ số đã tồn tại: {symbol}");
            }
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        var entities = prepared
            .Select(x => MapToMasterEntity(x.Request, x.Symbol))
            .ToList();

        foreach (var entity in entities)
        {
            _unitOfWork.LabIndicators.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), entities.Select(e => _mapper.Map<LabIndicatorResponse>(e)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorResponse? Data)> UpdateLabIndicatorAsync(
        Guid id,
        UpdateLabIndicatorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id chỉ số xét nghiệm không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" }, null);
        }

        var indicator = await _unitOfWork.LabIndicators.GetByIdAsync(id, cancellationToken);
        if (indicator is null || indicator.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy chỉ số xét nghiệm" }, null);
        }

        if (request.Symbol is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return (false, false, new[] { "Symbol không được để trống" }, null);
            }

            var newSymbol = NormalizeSymbol(request.Symbol)!;
            if (await _unitOfWork.LabIndicators.SymbolExistsAsync(newSymbol, id, cancellationToken))
            {
                return (false, false, new[] { "Ký hiệu chỉ số đã tồn tại" }, null);
            }

            indicator.Symbol = newSymbol;
        }

        if (request.FullName is not null)
        {
            indicator.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        }

        if (request.Unit is not null)
        {
            indicator.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        }

        if (request.Description is not null)
        {
            indicator.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.Category is not null)
        {
            indicator.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        }

        if (request.MinReference.HasValue)
        {
            indicator.MinReference = request.MinReference;
        }

        if (request.MaxReference.HasValue)
        {
            indicator.MaxReference = request.MaxReference;
        }

        if (request.IsActive.HasValue)
        {
            indicator.IsActive = request.IsActive.Value;
        }

        var rangeErrors = ValidateFallbackRange(indicator.MinReference, indicator.MaxReference);
        if (rangeErrors.Count > 0)
        {
            return (false, false, rangeErrors, null);
        }

        indicator.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.LabIndicators.Update(indicator);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorResponse>(indicator));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteLabIndicatorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id chỉ số xét nghiệm không hợp lệ" });
        }

        var indicator = await _unitOfWork.LabIndicators.GetByIdAsync(id, cancellationToken);
        if (indicator is null || indicator.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy chỉ số xét nghiệm" });
        }

        var utcNow = DateTime.UtcNow;
        indicator.IsDeleted = true;
        indicator.DeletedAt = utcNow;
        indicator.UpdatedAt = utcNow;
        indicator.IsActive = false;

        _unitOfWork.LabIndicators.Update(indicator);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAliasResponse>? Data)> GetAliasesByIndicatorIdAsync(
        Guid indicatorId,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        var aliases = await _unitOfWork.LabIndicatorAliases.GetAllAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId,
            query => query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.AliasText),
            cancellationToken);

        return (true, false, Array.Empty<string>(), aliases.Select(a => _mapper.Map<LabIndicatorAliasResponse>(a)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorReferenceRangeResponse>? Data)> GetReferenceRangesByIndicatorIdAsync(
        Guid indicatorId,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        var ranges = await _unitOfWork.LabIndicatorReferenceRanges.GetAllAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId,
            query => query.OrderBy(x => x.Gender).ThenBy(x => x.AgeGroup).ThenBy(x => x.CreatedAt),
            cancellationToken);

        return (true, false, Array.Empty<string>(), ranges.Select(r => _mapper.Map<LabIndicatorReferenceRangeResponse>(r)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAdviceCacheResponse>? Data)> GetAdviceCachesByIndicatorIdAsync(
        Guid indicatorId,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        var adviceCaches = await _unitOfWork.LabIndicatorAdviceCaches.GetAllAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId,
            query => query.OrderBy(x => x.Status),
            cancellationToken);

        return (true, false, Array.Empty<string>(), adviceCaches.Select(a => _mapper.Map<LabIndicatorAdviceCacheResponse>(a)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAliasResponse>? Data)> BulkCreateAliasesAsync(
        Guid indicatorId,
        BulkCreateLabIndicatorAliasesRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (request is null || request.Aliases is null || request.Aliases.Count == 0)
        {
            return (false, false, new[] { "At least one alias is required." }, null);
        }

        var validationErrors = new List<string>();
        var normalizedAliases = new List<(string AliasText, string? Language, bool IsPrimary)>();

        for (var index = 0; index < request.Aliases.Count; index++)
        {
            var alias = request.Aliases[index];
            if (string.IsNullOrWhiteSpace(alias.AliasText))
            {
                validationErrors.Add($"Aliases[{index}]: AliasText là bắt buộc");
                continue;
            }

            normalizedAliases.Add((
                alias.AliasText.Trim(),
                string.IsNullOrWhiteSpace(alias.Language) ? null : alias.Language.Trim(),
                alias.IsPrimary));
        }

        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        var duplicateInRequest = normalizedAliases
            .GroupBy(x => x.AliasText, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateInRequest.Count > 0)
        {
            return (false, false, duplicateInRequest.Select(text => $"Duplicate alias in request: {text}"), null);
        }

        foreach (var aliasText in normalizedAliases.Select(x => x.AliasText))
        {
            var exists = await _unitOfWork.LabIndicatorAliases.FirstOrDefaultAsync(
                x => !x.IsDeleted
                     && x.IndicatorId == indicatorId
                     && x.AliasText.ToLower() == aliasText.ToLower(),
                cancellationToken: cancellationToken);

            if (exists is not null)
            {
                validationErrors.Add($"Alias đã tồn tại cho chỉ số này: {aliasText}");
            }
        }

        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        var utcNow = DateTime.UtcNow;
        var entities = normalizedAliases.Select(alias => new LabIndicatorAlias
        {
            Id = Guid.NewGuid(),
            IndicatorId = indicatorId,
            AliasText = alias.AliasText,
            Language = alias.Language,
            IsPrimary = alias.IsPrimary,
            CreatedAt = utcNow,
        }).ToList();

        foreach (var entity in entities)
        {
            _unitOfWork.LabIndicatorAliases.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), entities.Select(e => _mapper.Map<LabIndicatorAliasResponse>(e)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAliasResponse? Data)> CreateAliasAsync(
        Guid indicatorId,
        CreateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AliasText))
        {
            return (false, false, new[] { "AliasText là bắt buộc" }, null);
        }

        var aliasText = request.AliasText.Trim();
        var exists = await _unitOfWork.LabIndicatorAliases.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && x.IndicatorId == indicatorId
                 && x.AliasText.ToLower() == aliasText.ToLower(),
            cancellationToken: cancellationToken);

        if (exists is not null)
        {
            return (false, false, new[] { $"Alias đã tồn tại cho chỉ số này: {aliasText}" }, null);
        }

        var entity = new LabIndicatorAlias
        {
            Id = Guid.NewGuid(),
            IndicatorId = indicatorId,
            AliasText = aliasText,
            Language = NormalizeOptionalText(request.Language),
            IsPrimary = request.IsPrimary,
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.LabIndicatorAliases.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorAliasResponse>(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAliasResponse? Data)> UpdateAliasAsync(
        Guid indicatorId,
        Guid aliasId,
        UpdateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (aliasId == Guid.Empty)
        {
            return (false, false, new[] { "Id alias không hợp lệ" }, null);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AliasText))
        {
            return (false, false, new[] { "AliasText là bắt buộc" }, null);
        }

        var alias = await _unitOfWork.LabIndicatorAliases.GetByIdAsync(aliasId, cancellationToken);
        if (alias is null || alias.IsDeleted || alias.IndicatorId != indicatorId)
        {
            return (false, true, new[] { "Không tìm thấy alias" }, null);
        }

        var aliasText = request.AliasText.Trim();
        var duplicate = await _unitOfWork.LabIndicatorAliases.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && x.IndicatorId == indicatorId
                 && x.Id != aliasId
                 && x.AliasText.ToLower() == aliasText.ToLower(),
            cancellationToken: cancellationToken);

        if (duplicate is not null)
        {
            return (false, false, new[] { $"Alias đã tồn tại cho chỉ số này: {aliasText}" }, null);
        }

        alias.AliasText = aliasText;
        alias.Language = NormalizeOptionalText(request.Language);
        alias.IsPrimary = request.IsPrimary;
        alias.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.LabIndicatorAliases.Update(alias);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorAliasResponse>(alias));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAliasAsync(
        Guid indicatorId,
        Guid aliasId,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors);
        }

        if (aliasId == Guid.Empty)
        {
            return (false, false, new[] { "Id alias không hợp lệ" });
        }

        var alias = await _unitOfWork.LabIndicatorAliases.GetByIdAsync(aliasId, cancellationToken);
        if (alias is null || alias.IsDeleted || alias.IndicatorId != indicatorId)
        {
            return (false, true, new[] { "Không tìm thấy alias" });
        }

        var utcNow = DateTime.UtcNow;
        alias.IsDeleted = true;
        alias.DeletedAt = utcNow;
        alias.UpdatedAt = utcNow;

        _unitOfWork.LabIndicatorAliases.Update(alias);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorReferenceRangeResponse>? Data)> BulkCreateReferenceRangesAsync(
        Guid indicatorId,
        BulkCreateLabIndicatorReferenceRangesRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (request is null || request.ReferenceRanges is null || request.ReferenceRanges.Count == 0)
        {
            return (false, false, new[] { "At least one reference range is required." }, null);
        }

        var validationErrors = new List<string>();
        var entities = new List<LabIndicatorReferenceRange>();
        var utcNow = DateTime.UtcNow;

        var existingRanges = await _unitOfWork.LabIndicatorReferenceRanges.GetAllAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId,
            cancellationToken: cancellationToken);

        for (var index = 0; index < request.ReferenceRanges.Count; index++)
        {
            var range = request.ReferenceRanges[index];
            var rangeErrors = ValidateReferenceRange(range);
            foreach (var rangeError in rangeErrors)
            {
                validationErrors.Add($"ReferenceRanges[{index}]: {rangeError}");
            }

            if (rangeErrors.Count > 0)
            {
                continue;
            }

            var uniquenessErrors = ValidateReferenceRangeUniqueness(
                range.Gender,
                range.AgeGroup,
                existingRanges.Concat(entities).ToList());
            foreach (var uniquenessError in uniquenessErrors)
            {
                validationErrors.Add($"ReferenceRanges[{index}]: {uniquenessError}");
            }

            if (uniquenessErrors.Count > 0)
            {
                continue;
            }

            entities.Add(new LabIndicatorReferenceRange
            {
                Id = Guid.NewGuid(),
                IndicatorId = indicatorId,
                Gender = range.Gender,
                AgeGroup = range.AgeGroup,
                ComparisonType = range.ComparisonType,
                MinValue = range.MinValue,
                MaxValue = range.MaxValue,
                Unit = string.IsNullOrWhiteSpace(range.Unit) ? null : range.Unit.Trim(),
                CreatedAt = utcNow,
            });
        }

        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        foreach (var entity in entities)
        {
            _unitOfWork.LabIndicatorReferenceRanges.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), entities.Select(e => _mapper.Map<LabIndicatorReferenceRangeResponse>(e)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorReferenceRangeResponse? Data)> CreateReferenceRangeAsync(
        Guid indicatorId,
        CreateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request khoảng tham chiếu là bắt buộc" }, null);
        }

        var rangeErrors = ValidateReferenceRange(request);
        if (rangeErrors.Count > 0)
        {
            return (false, false, rangeErrors, null);
        }

        var existingRanges = await _unitOfWork.LabIndicatorReferenceRanges.GetAllAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId,
            cancellationToken: cancellationToken);

        var uniquenessErrors = ValidateReferenceRangeUniqueness(request.Gender, request.AgeGroup, existingRanges);
        if (uniquenessErrors.Count > 0)
        {
            return (false, false, uniquenessErrors, null);
        }

        var entity = new LabIndicatorReferenceRange
        {
            Id = Guid.NewGuid(),
            IndicatorId = indicatorId,
            Gender = request.Gender,
            AgeGroup = request.AgeGroup,
            ComparisonType = request.ComparisonType,
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            Unit = NormalizeOptionalText(request.Unit),
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.LabIndicatorReferenceRanges.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorReferenceRangeResponse>(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorReferenceRangeResponse? Data)> UpdateReferenceRangeAsync(
        Guid indicatorId,
        Guid referenceRangeId,
        UpdateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (referenceRangeId == Guid.Empty)
        {
            return (false, false, new[] { "Id khoảng tham chiếu không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request khoảng tham chiếu là bắt buộc" }, null);
        }

        var range = await _unitOfWork.LabIndicatorReferenceRanges.GetByIdAsync(referenceRangeId, cancellationToken);
        if (range is null || range.IsDeleted || range.IndicatorId != indicatorId)
        {
            return (false, true, new[] { "Không tìm thấy khoảng tham chiếu" }, null);
        }

        var rangeErrors = ValidateReferenceRange(request);
        if (rangeErrors.Count > 0)
        {
            return (false, false, rangeErrors, null);
        }

        var otherRanges = await _unitOfWork.LabIndicatorReferenceRanges.GetAllAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId && x.Id != referenceRangeId,
            cancellationToken: cancellationToken);

        var uniquenessErrors = ValidateReferenceRangeUniqueness(request.Gender, request.AgeGroup, otherRanges);
        if (uniquenessErrors.Count > 0)
        {
            return (false, false, uniquenessErrors, null);
        }

        range.Gender = request.Gender;
        range.AgeGroup = request.AgeGroup;
        range.ComparisonType = request.ComparisonType;
        range.MinValue = request.MinValue;
        range.MaxValue = request.MaxValue;
        range.Unit = NormalizeOptionalText(request.Unit);
        range.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.LabIndicatorReferenceRanges.Update(range);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorReferenceRangeResponse>(range));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteReferenceRangeAsync(
        Guid indicatorId,
        Guid referenceRangeId,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors);
        }

        if (referenceRangeId == Guid.Empty)
        {
            return (false, false, new[] { "Id khoảng tham chiếu không hợp lệ" });
        }

        var range = await _unitOfWork.LabIndicatorReferenceRanges.GetByIdAsync(referenceRangeId, cancellationToken);
        if (range is null || range.IsDeleted || range.IndicatorId != indicatorId)
        {
            return (false, true, new[] { "Không tìm thấy khoảng tham chiếu" });
        }

        var utcNow = DateTime.UtcNow;
        range.IsDeleted = true;
        range.DeletedAt = utcNow;
        range.UpdatedAt = utcNow;

        _unitOfWork.LabIndicatorReferenceRanges.Update(range);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAdviceCacheResponse>? Data)> BulkCreateAdviceCachesAsync(
        Guid indicatorId,
        BulkCreateLabIndicatorAdviceCachesRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (request is null || request.AdviceCaches is null || request.AdviceCaches.Count == 0)
        {
            return (false, false, new[] { "At least one advice cache entry is required." }, null);
        }

        var validationErrors = new List<string>();
        var entities = new List<LabIndicatorAdviceCache>();
        var utcNow = DateTime.UtcNow;

        for (var index = 0; index < request.AdviceCaches.Count; index++)
        {
            var advice = request.AdviceCaches[index];
            if (advice.Status == LabResultStatus.Unknown)
            {
                validationErrors.Add($"AdviceCaches[{index}]: Status không được là Unknown");
                continue;
            }

            var exists = await _unitOfWork.LabIndicatorAdviceCaches.FirstOrDefaultAsync(
                x => !x.IsDeleted && x.IndicatorId == indicatorId && x.Status == advice.Status,
                cancellationToken: cancellationToken);

            if (exists is not null)
            {
                validationErrors.Add($"Advice cache đã tồn tại cho status {advice.Status}.");
                continue;
            }

            var duplicateInBatch = entities.Any(x => x.Status == advice.Status);
            if (duplicateInBatch)
            {
                validationErrors.Add($"Duplicate status in request: {advice.Status}.");
                continue;
            }

            entities.Add(new LabIndicatorAdviceCache
            {
                Id = Guid.NewGuid(),
                IndicatorId = indicatorId,
                Status = advice.Status,
                DisplayTitle = NormalizeOptionalText(advice.DisplayTitle),
                Summary = NormalizeOptionalText(advice.Summary),
                PossibleCauses = NormalizeOptionalText(advice.PossibleCauses),
                LifestyleAdvice = NormalizeOptionalText(advice.LifestyleAdvice),
                NutritionalAdvice = NormalizeOptionalText(advice.NutritionalAdvice),
                UrgencyLevel = NormalizeOptionalText(advice.UrgencyLevel),
                SeverityLevel = advice.SeverityLevel,
                WarningSigns = NormalizeOptionalText(advice.WarningSigns),
                FollowUpSuggestion = NormalizeOptionalText(advice.FollowUpSuggestion),
                DoctorQuestions = NormalizeOptionalText(advice.DoctorQuestions),
                CreatedAt = utcNow,
            });
        }

        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        foreach (var entity in entities)
        {
            _unitOfWork.LabIndicatorAdviceCaches.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), entities.Select(e => _mapper.Map<LabIndicatorAdviceCacheResponse>(e)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAdviceCacheResponse? Data)> CreateAdviceCacheAsync(
        Guid indicatorId,
        CreateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request advice cache là bắt buộc" }, null);
        }

        if (request.Status == LabResultStatus.Unknown)
        {
            return (false, false, new[] { "Status không được là Unknown" }, null);
        }

        var exists = await _unitOfWork.LabIndicatorAdviceCaches.FirstOrDefaultAsync(
            x => !x.IsDeleted && x.IndicatorId == indicatorId && x.Status == request.Status,
            cancellationToken: cancellationToken);

        if (exists is not null)
        {
            return (false, false, new[] { $"Advice cache đã tồn tại cho status {request.Status}." }, null);
        }

        var entity = new LabIndicatorAdviceCache
        {
            Id = Guid.NewGuid(),
            IndicatorId = indicatorId,
            Status = request.Status,
            DisplayTitle = NormalizeOptionalText(request.DisplayTitle),
            Summary = NormalizeOptionalText(request.Summary),
            PossibleCauses = NormalizeOptionalText(request.PossibleCauses),
            LifestyleAdvice = NormalizeOptionalText(request.LifestyleAdvice),
            NutritionalAdvice = NormalizeOptionalText(request.NutritionalAdvice),
            UrgencyLevel = NormalizeOptionalText(request.UrgencyLevel),
            SeverityLevel = request.SeverityLevel,
            WarningSigns = NormalizeOptionalText(request.WarningSigns),
            FollowUpSuggestion = NormalizeOptionalText(request.FollowUpSuggestion),
            DoctorQuestions = NormalizeOptionalText(request.DoctorQuestions),
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.LabIndicatorAdviceCaches.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorAdviceCacheResponse>(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAdviceCacheResponse? Data)> UpdateAdviceCacheAsync(
        Guid indicatorId,
        Guid cacheId,
        UpdateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors, null);
        }

        if (cacheId == Guid.Empty)
        {
            return (false, false, new[] { "Id advice cache không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request advice cache là bắt buộc" }, null);
        }

        if (request.Status == LabResultStatus.Unknown)
        {
            return (false, false, new[] { "Status không được là Unknown" }, null);
        }

        var advice = await _unitOfWork.LabIndicatorAdviceCaches.GetByIdAsync(cacheId, cancellationToken);
        if (advice is null || advice.IsDeleted || advice.IndicatorId != indicatorId)
        {
            return (false, true, new[] { "Không tìm thấy advice cache" }, null);
        }

        var duplicate = await _unitOfWork.LabIndicatorAdviceCaches.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && x.IndicatorId == indicatorId
                 && x.Id != cacheId
                 && x.Status == request.Status,
            cancellationToken: cancellationToken);

        if (duplicate is not null)
        {
            return (false, false, new[] { $"Advice cache đã tồn tại cho status {request.Status}." }, null);
        }

        advice.Status = request.Status;
        advice.DisplayTitle = NormalizeOptionalText(request.DisplayTitle);
        advice.Summary = NormalizeOptionalText(request.Summary);
        advice.PossibleCauses = NormalizeOptionalText(request.PossibleCauses);
        advice.LifestyleAdvice = NormalizeOptionalText(request.LifestyleAdvice);
        advice.NutritionalAdvice = NormalizeOptionalText(request.NutritionalAdvice);
        advice.UrgencyLevel = NormalizeOptionalText(request.UrgencyLevel);
        advice.SeverityLevel = request.SeverityLevel;
        advice.WarningSigns = NormalizeOptionalText(request.WarningSigns);
        advice.FollowUpSuggestion = NormalizeOptionalText(request.FollowUpSuggestion);
        advice.DoctorQuestions = NormalizeOptionalText(request.DoctorQuestions);
        advice.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.LabIndicatorAdviceCaches.Update(advice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<LabIndicatorAdviceCacheResponse>(advice));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAdviceCacheAsync(
        Guid indicatorId,
        Guid cacheId,
        CancellationToken cancellationToken = default)
    {
        var (success, notFound, errors) = await ValidateIndicatorExistsAsync(indicatorId, cancellationToken);
        if (!success)
        {
            return (false, notFound, errors);
        }

        if (cacheId == Guid.Empty)
        {
            return (false, false, new[] { "Id advice cache không hợp lệ" });
        }

        var advice = await _unitOfWork.LabIndicatorAdviceCaches.GetByIdAsync(cacheId, cancellationToken);
        if (advice is null || advice.IsDeleted || advice.IndicatorId != indicatorId)
        {
            return (false, true, new[] { "Không tìm thấy advice cache" });
        }

        var utcNow = DateTime.UtcNow;
        advice.IsDeleted = true;
        advice.DeletedAt = utcNow;
        advice.UpdatedAt = utcNow;

        _unitOfWork.LabIndicatorAdviceCaches.Update(advice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private async Task<(bool Success, bool NotFound, List<string> Errors)> ValidateIndicatorExistsAsync(
        Guid indicatorId,
        CancellationToken cancellationToken)
    {
        if (indicatorId == Guid.Empty)
        {
            return (false, false, new List<string> { "Id chỉ số xét nghiệm không hợp lệ" });
        }

        var indicator = await _unitOfWork.LabIndicators.GetByIdAsync(indicatorId, cancellationToken);
        if (indicator is null || indicator.IsDeleted)
        {
            return (false, true, new List<string> { "Không tìm thấy chỉ số xét nghiệm" });
        }

        return (true, false, new List<string>());
    }

    private static LabIndicatorMaster MapToMasterEntity(CreateLabIndicatorRequest request, string symbol)
    {
        return new LabIndicatorMaster
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            FullName = NormalizeOptionalText(request.FullName),
            Unit = NormalizeOptionalText(request.Unit),
            MinReference = request.MinReference,
            MaxReference = request.MaxReference,
            Description = NormalizeOptionalText(request.Description),
            Category = NormalizeOptionalText(request.Category),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static List<string> ValidateIndicatorFields(string symbol, double? minReference, double? maxReference)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errors.Add("Symbol là bắt buộc");
        }

        errors.AddRange(ValidateFallbackRange(minReference, maxReference));
        return errors;
    }

    private static List<string> ValidateFallbackRange(double? minReference, double? maxReference)
    {
        if (minReference.HasValue && maxReference.HasValue && minReference.Value > maxReference.Value)
        {
            return new List<string> { "MinReference không được lớn hơn MaxReference" };
        }

        return new List<string>();
    }

    private static List<string> ValidateReferenceRange(CreateLabIndicatorReferenceRangeRequest range)
    {
        var errors = ValidateReferenceRangeValues(range.ComparisonType, range.MinValue, range.MaxValue);
        errors.AddRange(ValidateReferenceRangeDimensions(range.Gender, range.AgeGroup));
        return errors;
    }

    private static List<string> ValidateReferenceRange(UpdateLabIndicatorReferenceRangeRequest range)
    {
        var errors = ValidateReferenceRangeValues(range.ComparisonType, range.MinValue, range.MaxValue);
        errors.AddRange(ValidateReferenceRangeDimensions(range.Gender, range.AgeGroup));
        return errors;
    }

    private static List<string> ValidateReferenceRangeDimensions(Gender? gender, AgeGroup? ageGroup)
    {
        if (gender.HasValue && ageGroup.HasValue)
        {
            return new List<string> { "Khoảng tham chiếu không thể đặt cả Gender và AgeGroup" };
        }

        return new List<string>();
    }

    
    private static List<string> ValidateReferenceRangeUniqueness(
        Gender? gender,
        AgeGroup? ageGroup,
        IReadOnlyList<LabIndicatorReferenceRange> existingRanges)
    {
        var errors = new List<string>();

        if (gender.HasValue)
        {
            if (existingRanges.Any(r => r.Gender == gender))
            {
                errors.Add($"Khoảng tham chiếu cho giới tính {gender} đã tồn tại.");
            }
        }
        else if (ageGroup.HasValue)
        {
            if (existingRanges.Any(r => r.AgeGroup == ageGroup))
            {
                errors.Add($"Khoảng tham chiếu cho nhóm tuổi {ageGroup} đã tồn tại.");
            }
        }
        else if (existingRanges.Any(r => r.Gender is null && r.AgeGroup is null))
        {
            errors.Add("Khoảng tham chiếu mặc định đã tồn tại cho chỉ số này");
        }

        return errors;
    }

    private static List<string> ValidateReferenceRangeValues(
        ReferenceComparisonType comparisonType,
        double? minValue,
        double? maxValue)
    {
        return comparisonType switch
        {
            ReferenceComparisonType.Between when !minValue.HasValue || !maxValue.HasValue =>
                new List<string> { "So sánh Between yêu cầu MinValue và MaxValue" },
            ReferenceComparisonType.Between when minValue > maxValue =>
                new List<string> { "MinValue không được lớn hơn MaxValue" },
            ReferenceComparisonType.LessThanOrEqual when !maxValue.HasValue =>
                new List<string> { "So sánh LessThanOrEqual yêu cầu MaxValue" },
            ReferenceComparisonType.GreaterThanOrEqual when !minValue.HasValue =>
                new List<string> { "So sánh GreaterThanOrEqual yêu cầu MinValue" },
            _ => new List<string>(),
        };
    }

    private static string? NormalizeSymbol(string symbol)
    {
        return string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
