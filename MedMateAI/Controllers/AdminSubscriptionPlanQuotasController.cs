using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Quotas.Responses;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Requests;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[SubscriptionPlanQuotaValidationFilter]
[Route("api/admin")]
public sealed class AdminSubscriptionPlanQuotasController : ControllerBase
{
    private readonly ISubscriptionPlanQuotaService _quotaService;

    public AdminSubscriptionPlanQuotasController(
        ISubscriptionPlanQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    [HttpGet("quotas")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotaResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotaResponse>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotaResponse>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotaResponse>>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ListQuotaDefinitions(
        CancellationToken cancellationToken = default)
    {
        var result = await _quotaService.ListQuotaDefinitionsAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("subscription-plans/{planId:guid}/quotas")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanQuotaResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanQuotaResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanQuotaResponse>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanQuotaResponse>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanQuotaResponse>>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanQuotaResponse>>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ListPlanQuotas(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var result = await _quotaService.ListPlanQuotasAsync(planId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("subscription-plans/{planId:guid}/quotas/{quotaId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanQuotaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanQuotaResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanQuotaResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanQuotaResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanQuotaResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanQuotaResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertPlanQuota(
        Guid planId,
        Guid quotaId,
        [FromBody] UpsertSubscriptionPlanQuotaRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _quotaService.UpsertPlanQuotaAsync(
            planId,
            quotaId,
            request,
            cancellationToken);

        return this.ToActionResult(result, "Subscription plan quota saved.");
    }

    [HttpDelete("subscription-plans/{planId:guid}/quotas/{quotaId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePlanQuota(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default)
    {
        var result = await _quotaService.DeletePlanQuotaAsync(
            planId,
            quotaId,
            cancellationToken);

        return this.ToActionResult(result, "Subscription plan quota deleted.");
    }
}
