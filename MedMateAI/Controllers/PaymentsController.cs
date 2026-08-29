using System.Globalization;
using System.Text;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Payments.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private const string MobileDeepLinkScheme = "sep490mbmedicalaiassistant";
    private const string MobileDeepLinkHost = "payment-result";

    private readonly IPaymentService _paymentService;
    private readonly MobilePaymentOptions _mobilePaymentOptions;

    public PaymentsController(
        IPaymentService paymentService,
        IOptions<MobilePaymentOptions> mobilePaymentOptions)
    {
        _paymentService = paymentService;
        _mobilePaymentOptions = mobilePaymentOptions.Value;
    }

    // Legacy/backend testing only. Production payOS ReturnUrl should point to the FE route.
    [AllowAnonymous]
    [HttpGet("payos-return")]
    [ProducesResponseType(typeof(ApiResponse<PayOSReturnResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayOSReturn(CancellationToken cancellationToken = default)
    {
        var query = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var data = await _paymentService.ProcessPayOSReturnAsync(query, cancellationToken);

        return Ok(new ApiResponse<PayOSReturnResponse>
        {
            Success = data.Success,
            Message = data.Message,
            Data = data,
        });
    }

    // Legacy/backend testing only. Production payOS CancelUrl should point to the FE route.
    [AllowAnonymous]
    [HttpGet("payos-cancel")]
    [ProducesResponseType(typeof(ApiResponse<PayOSReturnResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayOSCancel(CancellationToken cancellationToken = default)
    {
        var query = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var data = await _paymentService.ProcessPayOSCancelAsync(query, cancellationToken);

        return Ok(new ApiResponse<PayOSReturnResponse>
        {
            Success = data.Success,
            Message = data.Message,
            Data = data,
        });
    }

    [AllowAnonymous]
    [HttpGet("mobile-return")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public IActionResult MobileReturn([FromQuery] string? orderCode)
    {
        return CreateMobileRedirect("return", orderCode);
    }

    [AllowAnonymous]
    [HttpGet("mobile-cancel")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public IActionResult MobileCancel([FromQuery] string? orderCode)
    {
        return CreateMobileRedirect("cancel", orderCode);
    }

    [AllowAnonymous]
    [HttpPost("payos-webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PayOSWebhook(CancellationToken cancellationToken = default)
    {
        var rawBody = await ReadRawBodyAsync(cancellationToken);
        var processed = await _paymentService.ProcessPayOSWebhookAsync(rawBody, cancellationToken);

        if (!processed)
        {
            return BadRequest("Invalid webhook");
        }

        return Ok("OK");
    }

    [AllowAnonymous]
    [HttpGet("payos-status/{orderCode:long}")]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayOSStatus(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        if (orderCode <= 0)
        {
            return BadRequest(new ApiResponse<PayOSPaymentStatusResponse>
            {
                Success = false,
                Message = "Invalid orderCode.",
            });
        }

        var data = await _paymentService.GetPayOSPaymentStatusAsync(orderCode, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<PayOSPaymentStatusResponse>
            {
                Success = false,
                Message = "Payment transaction not found.",
            });
        }

        return Ok(new ApiResponse<PayOSPaymentStatusResponse>
        {
            Success = true,
            Message = data.Message,
            Data = data,
        });
    }

    [Authorize]
    [HttpPost("payos-reconcile/{orderCode:long}")]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<PayOSPaymentStatusResponse>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ReconcilePayOSPayment(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _paymentService.ReconcilePayOSPaymentAsync(
            orderCode,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PaymentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _paymentService.GetAllPaymentsAsync(
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Ok(new ApiResponse<PagedResponse<PaymentResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PaymentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PaymentResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByUserId(
        Guid userId,
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<PagedResponse<PaymentResponse>>
            {
                Success = false,
                Message = "Invalid user id.",
            });
        }

        var data = await _paymentService.GetPaymentsByUserIdAsync(
            userId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Ok(new ApiResponse<PagedResponse<PaymentResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PaymentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PaymentResponse>>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPayments(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var (succeeded, errors, data) = await _paymentService.GetMyPaymentsAsync(
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!succeeded || data is null)
        {
            return Unauthorized(new ApiResponse<PagedResponse<PaymentResponse>>
            {
                Success = false,
                Message = "Unauthorized",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<PagedResponse<PaymentResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [Authorize]
    [HttpGet("me/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPaymentById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Invalid payment id.",
            });
        }

        var (succeeded, notFound, errors, data) = await _paymentService.GetMyPaymentByIdAsync(
            id,
            cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Payment not found.",
            });
        }

        if (!succeeded || data is null)
        {
            return Unauthorized(new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Unauthorized",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<PaymentResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Invalid payment id.",
            });
        }

        var data = await _paymentService.GetPaymentByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Payment not found.",
            });
        }

        return Ok(new ApiResponse<PaymentResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    private IActionResult CreateMobileRedirect(string result, string? orderCode)
    {
        if (!long.TryParse(
                orderCode,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedOrderCode)
            || parsedOrderCode <= 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid orderCode.",
            });
        }

        if (!IsValidMobileDeepLink(_mobilePaymentOptions.DeepLinkUrl))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ApiResponse<object>
                {
                    Success = false,
                    Message = "Mobile payment redirect is not configured.",
                });
        }

        var redirectUrl = QueryHelpers.AddQueryString(
            _mobilePaymentOptions.DeepLinkUrl.Trim(),
            new Dictionary<string, string?>
            {
                ["result"] = result,
                ["orderCode"] = parsedOrderCode.ToString(CultureInfo.InvariantCulture),
            });
        return Redirect(redirectUrl);
    }

    private static bool IsValidMobileDeepLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(
                uri.Scheme,
                MobileDeepLinkScheme,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                uri.Host,
                MobileDeepLinkHost,
                StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/");
    }

    private async Task<string> ReadRawBodyAsync(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;
        return body;
    }
}
