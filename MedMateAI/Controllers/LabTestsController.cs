using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabTests.Requests;
using MedMateAI.Application.DTOs.LabTests.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/lab-tests")]

public sealed class LabTestsController : ControllerBase
{
    private readonly ILabTestService _labTestService;
    private readonly IUserService _userService;

    public LabTestsController(ILabTestService labTestService, IUserService userService)
    {
        _labTestService = labTestService;
        _userService = userService;
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(ApiResponse<LabTestUploadResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<LabTestUploadResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabTestUploadResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Analyze(
        [FromBody] LabTestAnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await _userService.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized(new ApiResponse<LabTestUploadResponse>
            {
                Success = false,
                Message = "Unauthorized.",
            });
        }

        var (ok, errors, data) = await _labTestService.AnalyzeFromDocumentUrlAsync(
            currentUser.Id,
            request,
            cancellationToken);

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<LabTestUploadResponse>
            {
                Success = false,
                Message = "Lab test analyze request failed.",
                Errors = errors.ToList(),
                Data = data,
            });
        }

        return Accepted(new ApiResponse<LabTestUploadResponse>
        {
            Success = true,
            Message = "Lab test OCR queued.",
            Data = data,
        });
    }

    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabTestUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabTestUploadResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<LabTestUploadResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var currentUser = await _userService.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized(new ApiResponse<LabTestUploadResponse>
            {
                Success = false,
                Message = "Unauthorized.",
            });
        }

        if (sessionId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabTestUploadResponse>
            {
                Success = false,
                Message = "Invalid session id.",
            });
        }

        var data = await _labTestService.GetSessionAsync(currentUser.Id, sessionId, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<LabTestUploadResponse>
            {
                Success = false,
                Message = "Lab test session not found.",
            });
        }

        return Ok(new ApiResponse<LabTestUploadResponse>
        {
            Success = true,
            Message = data.Status switch
            {
                LabTestSessionStatus.Processing => "Lab test OCR is processing.",
                LabTestSessionStatus.Completed => "Lab test OCR completed.",
                LabTestSessionStatus.Failed => "Lab test OCR failed.",
                _ => "OK",
            },
            Data = data,
        });
    }
}
