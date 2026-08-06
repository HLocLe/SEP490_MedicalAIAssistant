using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Responses;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface IDepartmentConsultationQuestionService
{
    Task<PagedResponse<DepartmentConsultationQuestionResponse>> ListAsync(
        int pageNumber,
        int pageSize,
        Guid? departmentId = null,
        ConsultationQuestionCategory? category = null,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<DepartmentConsultationQuestionResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, DepartmentConsultationQuestionResponse? Data)> CreateAsync(
        CreateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<DepartmentConsultationQuestionResponse>? Data)> BulkCreateAsync(
        BulkCreateDepartmentConsultationQuestionsRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, DepartmentConsultationQuestionResponse? Data)> UpdateAsync(
        Guid id,
        UpdateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
