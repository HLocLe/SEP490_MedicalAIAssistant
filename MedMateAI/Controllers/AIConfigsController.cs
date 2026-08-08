using MedMateAI.Application.DTOs.AIConfigs.Requests;
using MedMateAI.Application.DTOs.AIConfigs.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/ai-configs")]
public sealed class AIConfigsController : ControllerBase
{
    private readonly IAIConfigService _aiConfigService;

    public AIConfigsController(IAIConfigService aiConfigService)
    {
        _aiConfigService = aiConfigService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AIConfigResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _aiConfigService.ListAIConfigsAsync(query.PageNumber, query.PageSize, cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AIConfigResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListActive(CancellationToken cancellationToken = default)
    {
        var data = await _aiConfigService.ListActiveAIConfigsAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("by-task-type/{taskType}")]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTaskType(string taskType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskType))
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("TaskType là bắt buộc"));
        }

        var data = await _aiConfigService.GetActiveAIConfigByTaskTypeAsync(taskType, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<AIConfigResponse>("Không tìm thấy AI config"));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("Id AI config không hợp lệ"));
        }

        var data = await _aiConfigService.GetAIConfigByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<AIConfigResponse>("Không tìm thấy AI config"));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAIConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("Request body là bắt buộc", includeInErrors: true));
        }

        try
        {
            var data = await _aiConfigService.CreateAIConfigAsync(request, cancellationToken);
            return Ok(ApiResponseFactory.Success(data, "Tạo AI config thành công"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>(ex.Message, includeInErrors: true));
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAIConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("Id AI config không hợp lệ"));
        }

        if (request is null)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("Request body là bắt buộc", includeInErrors: true));
        }

        try
        {
            var data = await _aiConfigService.UpdateAIConfigAsync(id, request, cancellationToken);
            if (data is null)
            {
                return NotFound(ApiResponseFactory.Fail<AIConfigResponse>("Không tìm thấy AI config"));
            }

            return Ok(ApiResponseFactory.Success(data, "Cập nhật AI config thành công"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>(ex.Message, includeInErrors: true));
        }
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateAIConfigStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("Id AI config không hợp lệ"));
        }

        if (request is null)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>("Request body là bắt buộc", includeInErrors: true));
        }

        try
        {
            var data = await _aiConfigService.UpdateAIConfigStatusAsync(id, request, cancellationToken);
            if (data is null)
            {
                return NotFound(ApiResponseFactory.Fail<AIConfigResponse>("Không tìm thấy AI config"));
            }

            return Ok(ApiResponseFactory.Success(data, "Cập nhật trạng thái AI config thành công"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResponseFactory.Fail<AIConfigResponse>(ex.Message, includeInErrors: true));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<bool>("Id AI config không hợp lệ"));
        }

        var deleted = await _aiConfigService.DeleteAIConfigAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponseFactory.Fail<bool>("Không tìm thấy AI config"));
        }

        return Ok(ApiResponseFactory.Success(true, "Xóa AI config thành công"));
    }
}
