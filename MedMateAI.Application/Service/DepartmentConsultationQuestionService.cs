using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class DepartmentConsultationQuestionService : IDepartmentConsultationQuestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DepartmentConsultationQuestionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<DepartmentConsultationQuestionResponse>> ListAsync(
        int pageNumber,
        int pageSize,
        Guid? departmentId = null,
        ConsultationQuestionCategory? category = null,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var departmentIdFilter = departmentId.HasValue && departmentId.Value != Guid.Empty
            ? departmentId.Value
            : (Guid?)null;
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        var paged = await _unitOfWork.DepartmentConsultationQuestions.GetPagedAsync(
            pageNumber,
            pageSize,
            question => !question.IsDeleted
                && (!departmentIdFilter.HasValue || question.DepartmentId == departmentIdFilter.Value)
                && (!category.HasValue || question.Category == category.Value)
                && (!isActive.HasValue || question.IsActive == isActive.Value)
                && (searchTerm == null || question.QuestionText.ToLower().Contains(searchTerm)),
            query => query
                .OrderBy(question => question.DepartmentId)
                .ThenBy(question => question.Category)
                .ThenBy(question => question.SortOrder)
                .ThenBy(question => question.QuestionText),
            cancellationToken: cancellationToken);

        return new PagedResponse<DepartmentConsultationQuestionResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items
                .Select(question => _mapper.Map<DepartmentConsultationQuestionResponse>(question))
                .ToList(),
        };
    }

    public async Task<DepartmentConsultationQuestionResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var question = await _unitOfWork.DepartmentConsultationQuestions.GetByIdAsync(id, cancellationToken);
        if (question is null || question.IsDeleted)
        {
            return null;
        }

        return _mapper.Map<DepartmentConsultationQuestionResponse>(question);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, DepartmentConsultationQuestionResponse? Data)> CreateAsync(
        CreateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body là bắt buộc" }, null);
        }

        var validationErrors = await ValidateCreateFieldsAsync(request, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var entity = MapToEntity(request);
        _unitOfWork.DepartmentConsultationQuestions.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), _mapper.Map<DepartmentConsultationQuestionResponse>(entity));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<DepartmentConsultationQuestionResponse>? Data)> BulkCreateAsync(
        BulkCreateDepartmentConsultationQuestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Questions is null || request.Questions.Count == 0)
        {
            return (false, new[] { "Cần ít nhất một câu hỏi" }, null);
        }

        var errors = new List<string>();
        var prepared = new List<DepartmentConsultationQuestion>();
        var seenInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Questions.Count; index++)
        {
            var item = request.Questions[index];
            if (item is null)
            {
                errors.Add($"Questions[{index}]: Mục câu hỏi là bắt buộc");
                continue;
            }

            var fieldErrors = await ValidateCreateFieldsAsync(item, cancellationToken, checkDbDuplicate: false);
            foreach (var fieldError in fieldErrors)
            {
                errors.Add($"Questions[{index}]: {fieldError}");
            }

            if (fieldErrors.Count > 0)
            {
                continue;
            }

            var questionText = item.QuestionText.Trim();
            var dedupeKey = $"{item.DepartmentId:D}|{questionText}";
            if (!seenInRequest.Add(dedupeKey))
            {
                errors.Add($"Questions[{index}]: Trùng câu hỏi trong request cho khoa này");
                continue;
            }

            prepared.Add(MapToEntity(item));
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        foreach (var group in prepared.GroupBy(x => x.DepartmentId))
        {
            var departmentId = group.Key;
            var texts = group.Select(x => x.QuestionText.ToLowerInvariant()).ToList();

            var existing = await _unitOfWork.DepartmentConsultationQuestions.GetAllAsync(
                x => !x.IsDeleted
                     && x.DepartmentId == departmentId
                     && texts.Contains(x.QuestionText.ToLower()),
                cancellationToken: cancellationToken);

            foreach (var duplicate in existing)
            {
                errors.Add($"Câu hỏi đã tồn tại cho khoa này: {duplicate.QuestionText}");
            }
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        foreach (var entity in prepared)
        {
            _unitOfWork.DepartmentConsultationQuestions.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (
            true,
            Array.Empty<string>(),
            prepared.Select(entity => _mapper.Map<DepartmentConsultationQuestionResponse>(entity)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, DepartmentConsultationQuestionResponse? Data)> UpdateAsync(
        Guid id,
        UpdateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id câu hỏi không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" }, null);
        }

        var entity = await _unitOfWork.DepartmentConsultationQuestions.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy câu hỏi tư vấn theo khoa" }, null);
        }

        var errors = new List<string>();

        if (request.DepartmentId.HasValue)
        {
            if (request.DepartmentId.Value == Guid.Empty)
            {
                errors.Add("Id khoa không hợp lệ");
            }
            else if (!await DepartmentExistsAsync(request.DepartmentId.Value, cancellationToken))
            {
                errors.Add("Không tìm thấy khoa");
            }
            else
            {
                entity.DepartmentId = request.DepartmentId.Value;
            }
        }

        if (request.Category.HasValue)
        {
            if (!Enum.IsDefined(typeof(ConsultationQuestionCategory), request.Category.Value))
            {
                errors.Add("Category không hợp lệ");
            }
            else
            {
                entity.Category = request.Category.Value;
            }
        }

        if (request.QuestionText is not null)
        {
            if (string.IsNullOrWhiteSpace(request.QuestionText))
            {
                errors.Add("QuestionText không được để trống");
            }
            else if (request.QuestionText.Trim().Length > 1000)
            {
                errors.Add("QuestionText không được vượt quá 1000 ký tự");
            }
            else
            {
                entity.QuestionText = request.QuestionText.Trim();
            }
        }

        if (request.SortOrder.HasValue && request.SortOrder.Value > 0)
        {
            entity.SortOrder = request.SortOrder.Value;
        }
        else
        {
            errors.Add("SortOrder phải lớn hơn 0");
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        if (errors.Count > 0)
        {
            return (false, false, errors, null);
        }

        var duplicate = await _unitOfWork.DepartmentConsultationQuestions.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && x.Id != id
                 && x.DepartmentId == entity.DepartmentId
                 && x.QuestionText.ToLower() == entity.QuestionText.ToLower(),
            cancellationToken: cancellationToken);

        if (duplicate is not null)
        {
            return (false, false, new[] { "Câu hỏi đã tồn tại cho khoa này" }, null);
        }

        entity.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.DepartmentConsultationQuestions.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<DepartmentConsultationQuestionResponse>(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id câu hỏi không hợp lệ" });
        }

        var entity = await _unitOfWork.DepartmentConsultationQuestions.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy câu hỏi tư vấn theo khoa" });
        }

        var utcNow = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;
        entity.IsActive = false;

        _unitOfWork.DepartmentConsultationQuestions.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private async Task<List<string>> ValidateCreateFieldsAsync(
        CreateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken,
        bool checkDbDuplicate = true)
    {
        var errors = new List<string>();

        if (request.DepartmentId == Guid.Empty)
        {
            errors.Add("Id khoa là bắt buộc");
        }
        else if (!await DepartmentExistsAsync(request.DepartmentId, cancellationToken))
        {
            errors.Add("Không tìm thấy khoa");
        }

        if (!Enum.IsDefined(typeof(ConsultationQuestionCategory), request.Category))
        {
            errors.Add("Category không hợp lệ");
        }

        if (request.SortOrder == 0)
        {
            errors.Add("SortOrder phải lớn hơn 0");
        }

        if (string.IsNullOrWhiteSpace(request.QuestionText))
        {
            errors.Add("QuestionText là bắt buộc");
        }
        else if (request.QuestionText.Trim().Length > 1000)
        {
            errors.Add("QuestionText không được vượt quá 1000 ký tự");
        }

        if (errors.Count > 0 || !checkDbDuplicate || request.DepartmentId == Guid.Empty)
        {
            return errors;
        }

        var questionText = request.QuestionText.Trim();
        var exists = await _unitOfWork.DepartmentConsultationQuestions.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && x.DepartmentId == request.DepartmentId
                 && x.QuestionText.ToLower() == questionText.ToLower(),
            cancellationToken: cancellationToken);

        if (exists is not null)
        {
            errors.Add("Câu hỏi đã tồn tại cho khoa này");
        }

        return errors;
    }

    private async Task<bool> DepartmentExistsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.MedicalDepartments.GetByIdAsync(departmentId, cancellationToken);
        return department is not null && !department.IsDeleted;
    }

    private static DepartmentConsultationQuestion MapToEntity(CreateDepartmentConsultationQuestionRequest request)
    {
        return new DepartmentConsultationQuestion
        {
            Id = Guid.NewGuid(),
            DepartmentId = request.DepartmentId,
            Category = request.Category,
            QuestionText = request.QuestionText.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
