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
            return BadRequest(new ApiResponse<PagedResponse<ClinicalQuestionResponse>>
            {
                Success = false,
                Message = "Id ICD chapter không hợp lệ",
            });
        }

        var data = await _clinicalQuestionService.ListClinicalQuestionsAsync(
            query.PageNumber,
            query.PageSize,
            chapterId,
            search,
            cancellationToken);

        return Ok(new ApiResponse<PagedResponse<ClinicalQuestionResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<ClinicalQuestionResponse>
            {
                Success = false,
                Message = "Id câu hỏi không hợp lệ",
            });
        }

        var data = await _clinicalQuestionService.GetClinicalQuestionByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<ClinicalQuestionResponse>
            {
                Success = false,
                Message = "Không tìm thấy câu hỏi lâm sàng",
            });
        }

        return Ok(new ApiResponse<ClinicalQuestionResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
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
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<ClinicalQuestionResponse>
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Tạo câu hỏi lâm sàng thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<ClinicalQuestionResponse>
        {
            Success = true,
            Message = "Tạo câu hỏi lâm sàng thành công",
            Data = data,
        });
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
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<IReadOnlyList<ClinicalQuestionResponse>>
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Tạo hàng loạt câu hỏi lâm sàng thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<ClinicalQuestionResponse>>
        {
            Success = true,
            Message = $"Tạo thành công {data.Count} câu hỏi lâm sàng",
            Data = data,
        });
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
            return BadRequest(new ApiResponse<ClinicalQuestionResponse>
            {
                Success = false,
                Message = "Id câu hỏi không hợp lệ",
            });
        }

        var (ok, notFound, errors, data) = await _clinicalQuestionService.UpdateClinicalQuestionAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<ClinicalQuestionResponse>
            {
                Success = false,
                Message = "Không tìm thấy câu hỏi lâm sàng",
            });
        }

        if (!ok || data is null)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<ClinicalQuestionResponse>
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Cập nhật câu hỏi lâm sàng thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<ClinicalQuestionResponse>
        {
            Success = true,
            Message = "Cập nhật câu hỏi lâm sàng thành công",
            Data = data,
        });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Id câu hỏi không hợp lệ",
            });
        }

        var (ok, notFound, errors) = await _clinicalQuestionService.SoftDeleteClinicalQuestionAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = "Không tìm thấy câu hỏi lâm sàng",
            });
        }

        if (!ok)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Xóa câu hỏi lâm sàng thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Xóa câu hỏi lâm sàng thành công",
        });
    }
}
