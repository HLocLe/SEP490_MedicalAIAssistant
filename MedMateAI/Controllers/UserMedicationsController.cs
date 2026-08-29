using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.UserMedications;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.UserMedications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/user-medications")]
public sealed class UserMedicationsController : ControllerBase
{
    private readonly IUserMedicationService _service;

    public UserMedicationsController(IUserMedicationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return await WithUser(userId =>
            _service.GetMineAsync(userId, cancellationToken));
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetMinePaged(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
    {
        return await WithUser(userId =>
            _service.GetMinePagedAsync(userId, query, cancellationToken));
    }

    [HttpGet("{medicationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid medicationId,
        CancellationToken cancellationToken)
    {
        return await WithUser(userId =>
            _service.GetByIdAsync(userId, medicationId, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserMedicationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return this.MedicationUnauthorizedResult();
        }

        var result = await _service.CreateAsync(
            userId,
            request,
            cancellationToken);
        return this.ToCreatedResult(result);
    }

    [HttpPut("{medicationId:guid}")]
    public async Task<IActionResult> Update(
        Guid medicationId,
        [FromBody] UpdateUserMedicationRequest request,
        CancellationToken cancellationToken)
    {
        return await WithUser(userId =>
            _service.UpdateAsync(
                userId,
                medicationId,
                request,
                cancellationToken));
    }

    [HttpDelete("{medicationId:guid}")]
    public async Task<IActionResult> Delete(
        Guid medicationId,
        CancellationToken cancellationToken)
    {
        return await WithUser(userId =>
            _service.DeleteAsync(userId, medicationId, cancellationToken));
    }

    [HttpPut("{medicationId:guid}/reminders")]
    public async Task<IActionResult> ReplaceReminderTimes(
        Guid medicationId,
        [FromBody] ReplaceMedicationReminderTimesRequest request,
        CancellationToken cancellationToken)
    {
        return await WithUser(userId =>
            _service.ReplaceReminderTimesAsync(
                userId,
                medicationId,
                request,
                cancellationToken));
    }

    private async Task<IActionResult> WithUser<T>(
        Func<Guid, Task<UserMedicationOperationResult<T>>> action)
    {
        if (!TryGetUserId(out var userId))
        {
            return this.MedicationUnauthorizedResult();
        }

        var result = await action(userId);
        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
