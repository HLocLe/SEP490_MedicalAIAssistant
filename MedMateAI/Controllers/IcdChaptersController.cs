using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.IcdChapters.Requests;
using MedMateAI.Application.DTOs.IcdChapters.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/icd-chapters")]
public sealed class IcdChaptersController : ControllerBase
{
    private const string InvalidIdMessage = "Id ICD chapter không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy ICD chapter";

    private readonly IIcdChapterService _icdChapterService;

    public IcdChaptersController(IIcdChapterService icdChapterService)
    {
        _icdChapterService = icdChapterService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<IcdChapterResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var data = await _icdChapterService.ListIcdChaptersAsync(
            query.PageNumber,
            query.PageSize,
            search,
            cancellationToken);

        return Ok(Success(data, "OK"));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail<IcdChapterResponse>(InvalidIdMessage));
        }

        var data = await _icdChapterService.GetIcdChapterByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(Fail<IcdChapterResponse>(NotFoundMessage));
        }

        return Ok(Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateIcdChapterRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _icdChapterService.CreateIcdChapterAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(FailFromErrors<IcdChapterResponse>(errors, "Tạo ICD chapter thất bại"));
        }

        return Ok(Success(data, "Tạo ICD chapter thành công"));
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IcdChapterResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IcdChapterResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkCreate(
        [FromBody] BulkCreateIcdChaptersRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _icdChapterService.BulkCreateIcdChaptersAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(FailFromErrors<IReadOnlyList<IcdChapterResponse>>(errors, "Bulk create ICD chapters failed."));
        }

        return Ok(Success(data, $"{data.Count} ICD chapter(s) created."));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IcdChapterResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateIcdChapterRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail<IcdChapterResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _icdChapterService.UpdateIcdChapterAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(Fail<IcdChapterResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(FailFromErrors<IcdChapterResponse>(errors, "Cập nhật ICD chapter thất bại"));
        }

        return Ok(Success(data, "Cập nhật ICD chapter thành công"));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _icdChapterService.SoftDeleteIcdChapterAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(Fail(NotFoundMessage));
        }

        if (!ok)
        {
            return BadRequest(FailFromErrors(errors, "Xóa ICD chapter thất bại"));
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Xóa ICD chapter thành công",
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
