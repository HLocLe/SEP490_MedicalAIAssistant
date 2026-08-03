using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.SubscriptionPlans.Requests;
using MedMateAI.Application.DTOs.SubscriptionPlans.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/subscription-plans")]
public sealed class SubscriptionPlansController : ControllerBase
{
    private readonly ISubscriptionPlanService _subscriptionPlanService;

    public SubscriptionPlansController(ISubscriptionPlanService subscriptionPlanService)
    {
        _subscriptionPlanService = subscriptionPlanService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        var data = await _subscriptionPlanService.ListSubscriptionPlansAsync(cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<SubscriptionPlanResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListActive(CancellationToken cancellationToken = default)
    {
        var data = await _subscriptionPlanService.ListActiveSubscriptionPlansAsync(cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<SubscriptionPlanResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Invalid subscription plan id.",
            });
        }

        var data = await _subscriptionPlanService.GetSubscriptionPlanByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Subscription plan not found.",
            });
        }

        return Ok(new ApiResponse<SubscriptionPlanResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Create subscription plan failed.",
                Errors = new List<string> { "Request body is required." },
            });
        }

        try
        {
            var data = await _subscriptionPlanService.CreateSubscriptionPlanAsync(request, cancellationToken);
            return Ok(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = true,
                Message = "Subscription plan created.",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Create subscription plan failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Create subscription plan failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Invalid subscription plan id.",
            });
        }

        if (request is null)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Update subscription plan failed.",
                Errors = new List<string> { "Request body is required." },
            });
        }

        try
        {
            var data = await _subscriptionPlanService.UpdateSubscriptionPlanAsync(id, request, cancellationToken);
            if (data is null)
            {
                return NotFound(new ApiResponse<SubscriptionPlanResponse>
                {
                    Success = false,
                    Message = "Subscription plan not found.",
                });
            }

            return Ok(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = true,
                Message = "Subscription plan updated.",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Update subscription plan failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Update subscription plan failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateSubscriptionPlanStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Invalid subscription plan id.",
            });
        }

        if (request is null)
        {
            return BadRequest(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Update subscription plan status failed.",
                Errors = new List<string> { "Request body is required." },
            });
        }

        var data = await _subscriptionPlanService.UpdateSubscriptionPlanStatusAsync(id, request, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<SubscriptionPlanResponse>
            {
                Success = false,
                Message = "Subscription plan not found.",
            });
        }

        return Ok(new ApiResponse<SubscriptionPlanResponse>
        {
            Success = true,
            Message = "Subscription plan status updated.",
            Data = data,
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "Invalid subscription plan id.",
            });
        }

        var deleted = await _subscriptionPlanService.DeleteSubscriptionPlanAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new ApiResponse<bool>
            {
                Success = false,
                Message = "Subscription plan not found.",
                Data = false,
            });
        }

        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = "Subscription plan deleted (soft).",
            Data = true,
        });
    }
}
