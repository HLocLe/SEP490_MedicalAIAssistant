using System.Security.Claims;
using MedMateAI.Application.DTOs.PushDevices;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/notifications/push-devices")]
public sealed class PushDevicesController : ControllerBase
{
    private readonly IUserPushDeviceService _service;

    public PushDevicesController(IUserPushDeviceService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPushDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return this.PushDeviceUnauthorizedResult();
        }

        var result = await _service.RegisterAsync(
            userId,
            request,
            cancellationToken);
        return this.ToPushDeviceActionResult(result);
    }

    [HttpDelete("{installationId}")]
    public async Task<IActionResult> Deactivate(
        string installationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return this.PushDeviceUnauthorizedResult();
        }

        var result = await _service.DeactivateAsync(
            userId,
            installationId,
            cancellationToken);
        return this.ToPushDeviceActionResult(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
