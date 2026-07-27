using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/recovery-plan-requests")]
public sealed class RecoveryPlanRequestsController : ControllerBase
{
    private readonly IRecoveryPlanRequestService _service;
    public RecoveryPlanRequestsController(IRecoveryPlanRequestService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromHeader(Name = "Idempotency-Key")] string? key,
        [FromBody] CreateRecoveryPlanRequest request, CancellationToken token)
    {
        if (!TryUserId(out var userId)) return UnauthorizedResponse();
        var result = await _service.CreateAsync(userId, key ?? string.Empty, request, token);
        if (!result.Success) return Failure(result);
        var response = Success(result.Data!, result.IsReplay ? "Idempotent replay." : "Recovery plan request created.");
        return result.IsReplay ? Ok(response) : CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, response);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanRequestStatus? status, CancellationToken token)
    {
        if (!TryUserId(out var userId)) return UnauthorizedResponse();
        return Respond(await _service.GetMineAsync(userId, page, status, token));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken token)
    {
        if (!TryUserId(out var userId)) return UnauthorizedResponse();
        return Respond(await _service.GetDetailAsync(userId, User.IsInRole("Doctor"), User.IsInRole("Admin"), id, token));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken token)
    {
        if (!TryUserId(out var userId)) return UnauthorizedResponse();
        return Respond(await _service.CancelAsync(userId, id, token));
    }

    [HttpPost("{id:guid}/provide-more-information")]
    public async Task<IActionResult> ProvideInformation(Guid id, [FromBody] ProvideMoreInformationRequest request, CancellationToken token)
    {
        if (!TryUserId(out var userId)) return UnauthorizedResponse();
        return Respond(await _service.ProvideInformationAsync(userId, id, request.AdditionalInformation, token));
    }

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    private IActionResult Respond<T>(RecoveryPlanOperationResult<T> result) =>
        result.Success ? Ok(Success(result.Data!, result.IsReplay ? "Idempotent replay." : "OK")) : Failure(result);
    private IActionResult Failure<T>(RecoveryPlanOperationResult<T> result) =>
        StatusCode(ToStatus(result.Error), new ApiResponse<T>
        {
            Success = false, Message = result.Message ?? "Request failed.",
            Errors = new List<string> { ToCode(result.Error) }
        });
    private IActionResult UnauthorizedResponse() => Unauthorized(new ApiResponse
        { Success = false, Message = "Unauthorized.", Errors = new List<string> { "UNAUTHENTICATED" } });
    private static ApiResponse<T> Success<T>(T data, string message) => new() { Success = true, Message = message, Data = data };
    private static int ToStatus(RecoveryPlanErrorCode error) => error switch
    {
        RecoveryPlanErrorCode.Unauthenticated => 401,
        RecoveryPlanErrorCode.Forbidden or RecoveryPlanErrorCode.NoActiveSubscription
            or RecoveryPlanErrorCode.RecoveryPlanQuotaNotConfigured or RecoveryPlanErrorCode.DoctorNotActive
            or RecoveryPlanErrorCode.DoctorNotAcceptingRequests => 403,
        RecoveryPlanErrorCode.NotFound or RecoveryPlanErrorCode.DoctorProfileNotFound => 404,
        RecoveryPlanErrorCode.InvalidRequest or RecoveryPlanErrorCode.IdempotencyKeyInvalid => 400,
        _ => 409
    };
    private static string ToCode(RecoveryPlanErrorCode error) =>
        string.Concat(error.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? $"_{c}" : c.ToString())).ToUpperInvariant();
}
