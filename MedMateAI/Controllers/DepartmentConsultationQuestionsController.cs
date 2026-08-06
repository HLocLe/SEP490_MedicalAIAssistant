using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Enums;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/department-consultation-questions")]
public sealed class DepartmentConsultationQuestionsController : ControllerBase
{
    private const string InvalidIdMessage = "Id câu hỏi không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy câu hỏi tư vấn theo khoa";

    private readonly IDepartmentConsultationQuestionService _service;

    public DepartmentConsultationQuestionsController(IDepartmentConsultationQuestionService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DepartmentConsultationQuestionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DepartmentConsultationQuestionResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] Guid? departmentId,
        [FromQuery] ConsultationQuestionCategory? category,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken = default)
    {
        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<DepartmentConsultationQuestionResponse>>("Id khoa không hợp lệ"));
        }

        var data = await _service.ListAsync(
            query.PageNumber,
            query.PageSize,
            departmentId,
            category,
            search,
            isActive,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<DepartmentConsultationQuestionResponse>(InvalidIdMessage));
        }

        var data = await _service.GetByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<DepartmentConsultationQuestionResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _service.CreateAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<DepartmentConsultationQuestionResponse>(
                errors,
                "Tạo câu hỏi tư vấn theo khoa thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo câu hỏi tư vấn theo khoa thành công"));
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DepartmentConsultationQuestionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DepartmentConsultationQuestionResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkCreate(
        [FromBody] BulkCreateDepartmentConsultationQuestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _service.BulkCreateAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<DepartmentConsultationQuestionResponse>>(
                errors,
                "Tạo hàng loạt câu hỏi tư vấn theo khoa thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, $"Tạo thành công {data.Count} câu hỏi tư vấn theo khoa"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentConsultationQuestionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDepartmentConsultationQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<DepartmentConsultationQuestionResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _service.UpdateAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<DepartmentConsultationQuestionResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<DepartmentConsultationQuestionResponse>(
                errors,
                "Cập nhật câu hỏi tư vấn theo khoa thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật câu hỏi tư vấn theo khoa thành công"));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _service.SoftDeleteAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa câu hỏi tư vấn theo khoa thất bại",
            "Xóa câu hỏi tư vấn theo khoa thành công");
    }
}
