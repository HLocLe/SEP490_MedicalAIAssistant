using MedMateAI.Application.DTOs.ClinicalQuestions.Requests;
using MedMateAI.Application.DTOs.ClinicalQuestions.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/clinical-questions")]
public sealed class ClinicalQuestionsController : ControllerBase
{
    private const string InvalidIdMessage = "Id câu hỏi không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy câu hỏi lâm sàng";

    private readonly IClinicalQuestionService _clinicalQuestionService;

    public ClinicalQuestionsController(IClinicalQuestionService clinicalQuestionService)
    {
        _clinicalQuestionService = clinicalQuestionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ClinicalQuestionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ClinicalQuestionResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] Guid? chapterId,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        if (chapterId.HasValue && chapterId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<ClinicalQuestionResponse>>("Id ICD chapter không hợp lệ"));
        }

        var data = await _clinicalQuestionService.ListClinicalQuestionsAsync(
            query.PageNumber,
            query.PageSize,
            chapterId,
            search,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<ClinicalQuestionResponse>(InvalidIdMessage));
        }

        var data = await _clinicalQuestionService.GetClinicalQuestionByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<ClinicalQuestionResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateClinicalQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _clinicalQuestionService.CreateClinicalQuestionAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<ClinicalQuestionResponse>(errors, "Tạo câu hỏi lâm sàng thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo câu hỏi lâm sàng thành công"));
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClinicalQuestionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClinicalQuestionResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkCreate(
        [FromBody] BulkCreateClinicalQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _clinicalQuestionService.BulkCreateClinicalQuestionsAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<ClinicalQuestionResponse>>(errors, "Tạo hàng loạt câu hỏi lâm sàng thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, $"Tạo thành công {data.Count} câu hỏi lâm sàng"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateClinicalQuestionRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<ClinicalQuestionResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _clinicalQuestionService.UpdateClinicalQuestionAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<ClinicalQuestionResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<ClinicalQuestionResponse>(errors, "Cập nhật câu hỏi lâm sàng thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật câu hỏi lâm sàng thành công"));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _clinicalQuestionService.SoftDeleteClinicalQuestionAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa câu hỏi lâm sàng thất bại",
            "Xóa câu hỏi lâm sàng thành công");
    }
}
