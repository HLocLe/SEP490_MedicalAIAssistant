using MedMateAI.Application.DTOs.ClinicalQuestions.Requests;
using MedMateAI.Application.DTOs.ClinicalQuestions.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
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
            return BadRequest(Fail<PagedResponse<ClinicalQuestionResponse>>("Id ICD chapter không hợp lệ"));
        }

        var data = await _clinicalQuestionService.ListClinicalQuestionsAsync(
            query.PageNumber,
            query.PageSize,
            chapterId,
            search,
            cancellationToken);

        return Ok(Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail<ClinicalQuestionResponse>(InvalidIdMessage));
        }

        var data = await _clinicalQuestionService.GetClinicalQuestionByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(Fail<ClinicalQuestionResponse>(NotFoundMessage));
        }

        return Ok(Success(data, "OK"));
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
            return BadRequest(FailFromErrors<ClinicalQuestionResponse>(errors, "Tạo câu hỏi lâm sàng thất bại"));
        }

        return Ok(Success(data, "Tạo câu hỏi lâm sàng thành công"));
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
            return BadRequest(FailFromErrors<IReadOnlyList<ClinicalQuestionResponse>>(errors, "Tạo hàng loạt câu hỏi lâm sàng thất bại"));
        }

        return Ok(Success(data, $"Tạo thành công {data.Count} câu hỏi lâm sàng"));
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
            return BadRequest(Fail<ClinicalQuestionResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _clinicalQuestionService.UpdateClinicalQuestionAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(Fail<ClinicalQuestionResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(FailFromErrors<ClinicalQuestionResponse>(errors, "Cập nhật câu hỏi lâm sàng thất bại"));
        }

        return Ok(Success(data, "Cập nhật câu hỏi lâm sàng thành công"));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _clinicalQuestionService.SoftDeleteClinicalQuestionAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(Fail(NotFoundMessage));
        }

        if (!ok)
        {
            return BadRequest(FailFromErrors(errors, "Xóa câu hỏi lâm sàng thất bại"));
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Xóa câu hỏi lâm sàng thành công",
        });
    }

    private static ApiResponse<T> Success<T>(T data, string message) => new()
    {
        Success = true,
        Message = message,
        Data = data,
    };

    private static ApiResponse Fail(string message) => new()
    {
        Success = false,
        Message = message,
    };

    private static ApiResponse<T> Fail<T>(string message) => new()
    {
        Success = false,
        Message = message,
    };

    private static ApiResponse FailFromErrors(IEnumerable<string> errors, string fallbackMessage)
    {
        var errorList = errors.ToList();
        return new ApiResponse
        {
            Success = false,
            Message = errorList.FirstOrDefault() ?? fallbackMessage,
            Errors = errorList,
        };
    }

    private static ApiResponse<T> FailFromErrors<T>(IEnumerable<string> errors, string fallbackMessage)
    {
        var errorList = errors.ToList();
        return new ApiResponse<T>
        {
            Success = false,
            Message = errorList.FirstOrDefault() ?? fallbackMessage,
            Errors = errorList,
        };
    }
}
