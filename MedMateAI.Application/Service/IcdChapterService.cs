using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.IcdChapters.Requests;
using MedMateAI.Application.DTOs.IcdChapters.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
namespace MedMateAI.Application.Service;

public sealed class IcdChapterService : IIcdChapterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public IcdChapterService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<IcdChapterResponse>> ListIcdChaptersAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        var paged = await _unitOfWork.IcdChapters.GetPagedAsync(
            pageNumber,
            pageSize,
            chapter => !chapter.IsDeleted
                && (searchTerm == null
                    || chapter.ChapterCode.ToLower().Contains(searchTerm)
                    || chapter.ChapterName.ToLower().Contains(searchTerm)),
            query => query.OrderBy(chapter => chapter.ChapterCode),
            cancellationToken: cancellationToken);

        return new PagedResponse<IcdChapterResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(chapter => _mapper.Map<IcdChapterResponse>(chapter)).ToList(),
        };
    }

    public async Task<IcdChapterResponse?> GetIcdChapterByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var chapter = await _unitOfWork.IcdChapters.GetByIdAsync(id, cancellationToken);
        if (chapter is null || chapter.IsDeleted)
        {
            return null;
        }

        return _mapper.Map<IcdChapterResponse>(chapter);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, IcdChapterResponse? Data)> CreateIcdChapterAsync(
        CreateIcdChapterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body is required." }, null);
        }

        var validationErrors = ValidateChapterFields(request.ChapterCode, request.ChapterName);

        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var chapterCode = NormalizeChapterCode(request.ChapterCode)!;
        var chapterName = request.ChapterName.Trim();

        var existingChapter = await _unitOfWork.IcdChapters.FirstOrDefaultAsync(
            candidate => candidate.ChapterCode == chapterCode && !candidate.IsDeleted,
            cancellationToken: cancellationToken);

        if (existingChapter is not null)
        {
            return (false, new[] { "Chapter code already exists." }, null);
        }

        var newChapter = new IcdChapter
        {
            Id = Guid.NewGuid(),
            ChapterCode = chapterCode,
            ChapterName = chapterName,
            KeywordWeights = NormalizeKeywordWeights(request.KeywordWeights),
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.IcdChapters.Add(newChapter);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), _mapper.Map<IcdChapterResponse>(newChapter));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<IcdChapterResponse>? Data)> BulkCreateIcdChaptersAsync(
        BulkCreateIcdChaptersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Chapters is null || request.Chapters.Count == 0)
        {
            return (false, new[] { "At least one chapter is required." }, null);
        }

        var (preparationErrors, preparedChapters) = PrepareBulkIcdChapters(request);
        if (preparationErrors.Count > 0)
        {
            return (false, preparationErrors, null);
        }

        var duplicateErrors = GetDuplicateChapterCodeErrors(preparedChapters);
        if (duplicateErrors.Count > 0)
        {
            return (false, duplicateErrors, null);
        }

        var existingErrors = await GetExistingChapterCodeErrorsAsync(preparedChapters, cancellationToken);
        if (existingErrors.Count > 0)
        {
            return (false, existingErrors, null);
        }

        var responses = await PersistBulkIcdChaptersAsync(preparedChapters, cancellationToken);
        return (true, Array.Empty<string>(), responses);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IcdChapterResponse? Data)> UpdateIcdChapterAsync(
        Guid id,
        UpdateIcdChapterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid ICD chapter id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        if (request.ChapterName is not null && string.IsNullOrWhiteSpace(request.ChapterName))
        {
            return (false, false, new[] { "Chapter name cannot be empty when provided." }, null);
        }

        if (request.ChapterCode is not null && string.IsNullOrWhiteSpace(request.ChapterCode))
        {
            return (false, false, new[] { "Chapter code cannot be empty when provided." }, null);
        }

        if (request.ChapterName is null && request.KeywordWeights is null && request.ChapterCode is null)
        {
            return (false, false, new[] { "No fields to update." }, null);
        }

        var chapter = await _unitOfWork.IcdChapters.GetByIdAsync(id, cancellationToken);
        if (chapter is null || chapter.IsDeleted)
        {
            return (false, true, new[] { "ICD chapter not found." }, null);
        }

        var newCode = request.ChapterCode is not null ? NormalizeChapterCode(request.ChapterCode) : null;
        if (newCode is not null)
        {
            var validationErrors = ValidateChapterFields(newCode, request.ChapterName ?? chapter.ChapterName);
            if (validationErrors.Count > 0)
            {
                return (false, false, validationErrors, null);
            }
        }

        var codeChanging = newCode is not null
            && !string.Equals(chapter.ChapterCode, newCode, StringComparison.OrdinalIgnoreCase);

        if (codeChanging)
        {
            var exists = await _unitOfWork.IcdChapters.FirstOrDefaultAsync(
                c => c.ChapterCode == newCode && !c.IsDeleted && c.Id != id,
                cancellationToken: cancellationToken);

            if (exists is not null)
            {
                return (false, false, new[] { "Chapter code already exists." }, null);
            }
        }

        if (request.ChapterName is not null)
        {
            chapter.ChapterName = request.ChapterName.Trim();
        }

        if (request.KeywordWeights is not null)
        {
            chapter.KeywordWeights = NormalizeKeywordWeights(request.KeywordWeights);
        }

        chapter.UpdatedAt = DateTime.UtcNow;

        if (codeChanging)
        {
            await CascadeChapterCodeUpdateAsync(chapter, newCode!, cancellationToken);
        }
        else
        {
            _unitOfWork.IcdChapters.Update(chapter);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (true, false, Array.Empty<string>(), _mapper.Map<IcdChapterResponse>(chapter));
    }

    private async Task CascadeChapterCodeUpdateAsync(
        IcdChapter chapter,
        string newCode,
        CancellationToken cancellationToken)
    {
        var oldCode = chapter.ChapterCode;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var departmentIds = await _unitOfWork.MedicalDepartments.DetachChapterCodeAsync(oldCode, cancellationToken);

            await _unitOfWork.ClinicalQuestions.UpdateChapterCodeByChapterIdAsync(chapter.Id, newCode, cancellationToken);

            await _unitOfWork.IcdChapters.UpdateChapterCodeByIdAsync(chapter.Id, newCode, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.MedicalDepartments.AttachChapterCodeAsync(departmentIds, newCode, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            chapter.ChapterCode = newCode;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteIcdChapterAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid ICD chapter id." });
        }

        var chapter = await _unitOfWork.IcdChapters.GetByIdAsync(id, cancellationToken);
        if (chapter is null || chapter.IsDeleted)
        {
            return (false, true, new[] { "ICD chapter not found." });
        }

        chapter.IsDeleted = true;
        chapter.DeletedAt = DateTime.UtcNow;

        _unitOfWork.IcdChapters.Update(chapter);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    // Private method thuộc BulkCreateIcdChaptersAsync.
    private static (List<string> Errors, List<PreparedBulkIcdChapter> Chapters) PrepareBulkIcdChapters(
        BulkCreateIcdChaptersRequest request)
    {
        var errors = new List<string>();
        var preparedChapters = new List<PreparedBulkIcdChapter>();

        for (var index = 0; index < request.Chapters.Count; index++)
        {
            var chapterRequest = request.Chapters[index];
            if (chapterRequest is null)
            {
                errors.Add($"Chapters[{index}]: Item is required.");
                continue;
            }

            var fieldErrors = ValidateChapterFields(chapterRequest.ChapterCode, chapterRequest.ChapterName);
            foreach (var fieldError in fieldErrors)
            {
                errors.Add($"Chapters[{index}]: {fieldError}");
            }

            if (fieldErrors.Count > 0)
            {
                continue;
            }

            preparedChapters.Add(new PreparedBulkIcdChapter(
                index,
                NormalizeChapterCode(chapterRequest.ChapterCode)!,
                chapterRequest.ChapterName.Trim(),
                NormalizeKeywordWeights(chapterRequest.KeywordWeights)));
        }

        return (errors, preparedChapters);
    }

    // Private method thuộc BulkCreateIcdChaptersAsync.
    private static List<string> GetDuplicateChapterCodeErrors(IReadOnlyList<PreparedBulkIcdChapter> preparedChapters)
    {
        return preparedChapters
            .GroupBy(item => item.ChapterCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"Duplicate chapter code '{group.Key}' in request.")
            .ToList();
    }

    // Private method thuộc BulkCreateIcdChaptersAsync.
    private async Task<List<string>> GetExistingChapterCodeErrorsAsync(
        IReadOnlyList<PreparedBulkIcdChapter> preparedChapters,
        CancellationToken cancellationToken)
    {
        var allChapters = await _unitOfWork.IcdChapters.GetAllAsync(cancellationToken);
        var existingChapterCodes = allChapters
            .Where(chapter => !chapter.IsDeleted)
            .Select(chapter => chapter.ChapterCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return preparedChapters
            .Where(preparedChapter => existingChapterCodes.Contains(preparedChapter.ChapterCode))
            .Select(preparedChapter =>
                $"Chapters[{preparedChapter.RequestIndex}]: Chapter code '{preparedChapter.ChapterCode}' already exists.")
            .ToList();
    }

    // Private method thuộc BulkCreateIcdChaptersAsync.
    private async Task<IReadOnlyList<IcdChapterResponse>> PersistBulkIcdChaptersAsync(
        IReadOnlyList<PreparedBulkIcdChapter> preparedChapters,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = DateTime.UtcNow;
        var newChapters = preparedChapters
            .Select(preparedChapter => new IcdChapter
            {
                Id = Guid.NewGuid(),
                ChapterCode = preparedChapter.ChapterCode,
                ChapterName = preparedChapter.ChapterName,
                KeywordWeights = preparedChapter.KeywordWeights,
                CreatedAt = createdAtUtc,
            })
            .ToList();

        foreach (var chapter in newChapters)
        {
            _unitOfWork.IcdChapters.Add(chapter);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return newChapters
            .OrderBy(chapter => chapter.ChapterCode, StringComparer.OrdinalIgnoreCase)
            .Select(chapter => _mapper.Map<IcdChapterResponse>(chapter))
            .ToList();
    }

    private static List<string> ValidateChapterFields(string chapterCode, string chapterName)
    {
        var errors = new List<string>();

        if (NormalizeChapterCode(chapterCode) is null)
        {
            errors.Add("Chapter code is required.");
        }

        if (string.IsNullOrWhiteSpace(chapterName))
        {
            errors.Add("Chapter name is required.");
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

    private static Dictionary<string, int> NormalizeKeywordWeights(Dictionary<string, int>? keywordWeights)
    {
        if (keywordWeights is null || keywordWeights.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        return keywordWeights
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => pair.Key.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }
    
     private sealed record PreparedBulkIcdChapter(
        int RequestIndex,
        string ChapterCode,
        string ChapterName,
        Dictionary<string, int> KeywordWeights);

}
