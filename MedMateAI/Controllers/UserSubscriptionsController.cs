using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.UserSubscriptions.Requests;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/user-subscriptions")]
public sealed class UserSubscriptionsController : ControllerBase
{
    private const string UnauthenticatedError = "User is not authenticated.";
    private const string SaleOfferUnavailableError = "SALE_OFFER_UNAVAILABLE";

    private readonly IUserSubscriptionService _userSubscriptionService;

    public UserSubscriptionsController(IUserSubscriptionService userSubscriptionService)
    {
        _userSubscriptionService = userSubscriptionService;
    }

    [HttpGet("/api/admin/user-subscriptions")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResponse<UserSubscriptionResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResponse<UserSubscriptionResponse>>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminSubscriptions(
        [FromQuery] PaginationQuery pagination,
        [FromQuery] string? status,
        [FromQuery] bool currentOnly = false,
        CancellationToken cancellationToken = default)
    {
        var (succeeded, errors, data) = await _userSubscriptionService.GetAdminSubscriptionsAsync(
            pagination.PageNumber,
            pagination.PageSize,
            status,
            currentOnly,
            cancellationToken);

        if (!succeeded || data is null)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<PagedResponse<UserSubscriptionResponse>>
            {
                Success = false,
                Message = "List user subscriptions failed.",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<PagedResponse<UserSubscriptionResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSubscriptionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSubscriptionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSubscriptionResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSubscriptionResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new ApiResponse<CheckoutSubscriptionResponse>
            {
                Success = false,
                Message = "Checkout failed.",
                Errors = new List<string> { "Request body is required." },
            });
        }

        var (ok, errors, data) = await _userSubscriptionService.CheckoutAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            var errorList = errors.ToList();

            if (errorList.Contains(UnauthenticatedError, StringComparer.Ordinal))
            {
                return Unauthorized(new ApiResponse<CheckoutSubscriptionResponse>
                {
                    Success = false,
                    Message = "Unauthorized",
                    Errors = errorList,
                });
            }

            if (errorList.Contains(SaleOfferUnavailableError, StringComparer.Ordinal))
            {
                return Conflict(new ApiResponse<CheckoutSubscriptionResponse>
                {
                    Success = false,
                    Message = "The displayed sale offer is no longer available.",
                    Errors = errorList,
                });
            }

            return BadRequest(new ApiResponse<CheckoutSubscriptionResponse>
            {
                Success = false,
                Message = "Checkout failed.",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<CheckoutSubscriptionResponse>
        {
            Success = true,
            Message = "Checkout created.",
            Data = data,
        });
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserSubscriptionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserSubscriptionResponse>>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySubscriptions(CancellationToken cancellationToken = default)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new ApiResponse<IReadOnlyList<UserSubscriptionResponse>>
            {
                Success = false,
                Message = "Unauthorized",
                Errors = new List<string> { UnauthenticatedError },
            });
        }

        var data = await _userSubscriptionService.GetMySubscriptionsAsync(cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<UserSubscriptionResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<UserSubscriptionResponse>
            {
                Success = false,
                Message = "Invalid subscription id.",
            });
        }

        var data = await _userSubscriptionService.GetByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<UserSubscriptionResponse>
            {
                Success = false,
                Message = "User subscription not found.",
            });
        }

        return Ok(new ApiResponse<UserSubscriptionResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        var (ok, notFound, errors, data) = await _userSubscriptionService.CancelAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<UserSubscriptionResponse>
            {
                Success = false,
                Message = "User subscription not found.",
            });
        }

        if (!ok || data is null)
        {
            var errorList = errors.ToList();

            if (errorList.Contains(UnauthenticatedError, StringComparer.Ordinal))
            {
                return Unauthorized(new ApiResponse<UserSubscriptionResponse>
                {
                    Success = false,
                    Message = "Unauthorized",
                    Errors = errorList,
                });
            }

            return BadRequest(new ApiResponse<UserSubscriptionResponse>
            {
                Success = false,
                Message = "Cancel subscription failed.",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<UserSubscriptionResponse>
        {
            Success = true,
            Message = "Subscription cancelled.",
            Data = data,
        });
    }
}
