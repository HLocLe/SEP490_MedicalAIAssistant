using AutoMapper;
using MedMateAI.Application.DTOs.ClinicalQuestions.Requests;
using MedMateAI.Application.DTOs.ClinicalQuestions.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class ClinicalQuestionService : IClinicalQuestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ClinicalQuestionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<ClinicalQuestionResponse>> ListClinicalQuestionsAsync(
        int pageNumber,
        int pageSize,
        Guid? chapterId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var chapterIdFilter = chapterId.HasValue && chapterId.Value != Guid.Empty ? chapterId.Value : (Guid?)null;
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        var paged = await _unitOfWork.ClinicalQuestions.GetPagedAsync(
            pageNumber,
            pageSize,
            question => !question.IsDeleted
                && (!chapterIdFilter.HasValue || question.ChapterId == chapterIdFilter.Value)
                && (searchTerm == null
                    || question.QuestionVi.ToLower().Contains(searchTerm)
                    || (question.EnglishPrefix != null && question.EnglishPrefix.ToLower().Contains(searchTerm))),
            query => query.OrderBy(question => question.ChapterId).ThenBy(question => question.SortOrder),
            cancellationToken: cancellationToken);

        return new PagedResponse<ClinicalQuestionResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(question => _mapper.Map<ClinicalQuestionResponse>(question)).ToList(),
        };
    }

    public async Task<ClinicalQuestionResponse?> GetClinicalQuestionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var question = await _unitOfWork.ClinicalQuestions.GetByIdAsync(id, cancellationToken);
        if (question is null || question.IsDeleted)
        {
            return null;
        }

        return _mapper.Map<ClinicalQuestionResponse>(question);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, ClinicalQuestionResponse? Data)> CreateClinicalQuestionAsync(
        CreateClinicalQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body is required." }, null);
        }

        var validationErrors = await ValidateCreateFieldsAsync(request, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var entity = new ClinicalQuestion
        {
            Id = Guid.NewGuid(),
            ChapterId = request.ChapterId,
            ChapterCode = NormalizeChapterCode(request.ChapterCode),
            QuestionVi = request.QuestionVi.Trim(),
            EnglishPrefix = NormalizeOptionalText(request.EnglishPrefix),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.ClinicalQuestions.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), _mapper.Map<ClinicalQuestionResponse>(entity));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<ClinicalQuestionResponse>? Data)> BulkCreateClinicalQuestionsAsync(
        BulkCreateClinicalQuestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (request is null || request.Questions is null || request.Questions.Count == 0)
        {
            return (false, new[] { "At least one question is required." }, null);
        }

        var preparedQuestions = new List<(int RequestIndex, ClinicalQuestion Entity)>();

        for (var index = 0; index < request.Questions.Count; index++)
        {
            var questionRequest = request.Questions[index];
            if (questionRequest is null)
            {
                errors.Add($"Questions[{index}]: Item is required.");
                continue;
            }

            var fieldErrors = await ValidateCreateFieldsAsync(questionRequest, cancellationToken);
            foreach (var fieldError in fieldErrors)
            {
                errors.Add($"Questions[{index}]: {fieldError}");
            }

            if (fieldErrors.Count > 0)
            {
                continue;
            }

            preparedQuestions.Add((
                index,
                new ClinicalQuestion
                {
                    Id = Guid.NewGuid(),
                    ChapterId = questionRequest.ChapterId,
                    ChapterCode = NormalizeChapterCode(questionRequest.ChapterCode),
                    QuestionVi = questionRequest.QuestionVi.Trim(),
                    EnglishPrefix = NormalizeOptionalText(questionRequest.EnglishPrefix),
                    SortOrder = questionRequest.SortOrder,
                    CreatedAt = DateTime.UtcNow,
                }));
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        foreach (var preparedQuestion in preparedQuestions)
        {
            _unitOfWork.ClinicalQuestions.Add(preparedQuestion.Entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var responses = preparedQuestions
            .Select(preparedQuestion => _mapper.Map<ClinicalQuestionResponse>(preparedQuestion.Entity))
            .ToList();

        return (true, Array.Empty<string>(), responses);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, ClinicalQuestionResponse? Data)> UpdateClinicalQuestionAsync(
        Guid id,
        UpdateClinicalQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = GetUpdateValidationErrors(id, request);
        if (validationErrors is not null)
        {
            return (false, false, validationErrors, null);
        }

        var entity = await _unitOfWork.ClinicalQuestions.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Clinical question not found." }, null);
        }

        var chapterErrors = await ApplyChapterIdUpdateAsync(entity, request!, cancellationToken);
        if (chapterErrors is not null)
        {
            return (false, false, chapterErrors, null);
        }

        ApplyScalarUpdates(entity, request!);
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ClinicalQuestions.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<ClinicalQuestionResponse>(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteClinicalQuestionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid clinical question id." });
        }

        var entity = await _unitOfWork.ClinicalQuestions.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Clinical question not found." });
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        _unitOfWork.ClinicalQuestions.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    // Private method thuộc UpdateClinicalQuestionAsync.
    private static IEnumerable<string>? GetUpdateValidationErrors(Guid id, UpdateClinicalQuestionRequest? request)
    {
        if (id == Guid.Empty)
        {
            return new[] { "Invalid clinical question id." };
        }

        if (request is null)
        {
            return new[] { "Request body is required." };
        }

        if (request.QuestionVi is not null && string.IsNullOrWhiteSpace(request.QuestionVi))
        {
            return new[] { "Question text cannot be empty when provided." };
        }

        if (request.ChapterId is null
            && request.ChapterCode is null
            && request.QuestionVi is null
            && request.EnglishPrefix is null
            && request.SortOrder is null)
        {
            return new[] { "No fields to update." };
        }

        return null;
    }

    // Private method thuộc UpdateClinicalQuestionAsync.
    private async Task<IEnumerable<string>?> ApplyChapterIdUpdateAsync(
        ClinicalQuestion entity,
        UpdateClinicalQuestionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ChapterId is null)
        {
            return null;
        }

        if (request.ChapterId.Value == Guid.Empty)
        {
            return new[] { "Chapter id is invalid." };
        }

        var chapterExists = await _unitOfWork.ClinicalQuestions.IcdChapterExistsAsync(
            request.ChapterId.Value,
            cancellationToken);

        if (!chapterExists)
        {
            return new[] { "ICD chapter not found." };
        }

        entity.ChapterId = request.ChapterId.Value;
        return null;
    }

    // Private method thuộc UpdateClinicalQuestionAsync.
    private static void ApplyScalarUpdates(ClinicalQuestion entity, UpdateClinicalQuestionRequest request)
    {
        if (request.ChapterCode is not null)
        {
            entity.ChapterCode = NormalizeChapterCode(request.ChapterCode);
        }

        if (request.QuestionVi is not null)
        {
            entity.QuestionVi = request.QuestionVi.Trim();
        }

        if (request.EnglishPrefix is not null)
        {
            entity.EnglishPrefix = NormalizeOptionalText(request.EnglishPrefix);
        }

        if (request.SortOrder is not null)
        {
            entity.SortOrder = request.SortOrder.Value;
        }
    }

    private async Task<List<string>> ValidateCreateFieldsAsync(
        CreateClinicalQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (request.ChapterId == Guid.Empty)
        {
            errors.Add("Chapter id is required.");
        }
        else
        {
            var chapterExists = await _unitOfWork.ClinicalQuestions.IcdChapterExistsAsync(
                request.ChapterId,
                cancellationToken);

            if (!chapterExists)
            {
                errors.Add("ICD chapter not found.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.QuestionVi))
        {
            errors.Add("Question text is required.");
        }

        return errors;
    }

    private static string? NormalizeChapterCode(string? chapterCode)
    {
        if (string.IsNullOrWhiteSpace(chapterCode))
        {
            return null;
        }

        return chapterCode.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
