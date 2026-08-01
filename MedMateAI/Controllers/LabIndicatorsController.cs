using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabIndicators.Requests;
using MedMateAI.Application.DTOs.LabIndicators.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/lab-indicators")]

public sealed class LabIndicatorsController : ControllerBase
{
    private readonly ILabIndicatorService _labIndicatorService;

    public LabIndicatorsController(ILabIndicatorService labIndicatorService)
    {
        _labIndicatorService = labIndicatorService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<LabIndicatorResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var data = await _labIndicatorService.ListLabIndicatorsAsync(
            query.PageNumber,
            query.PageSize,
            search,
            cancellationToken);

        return Ok(new ApiResponse<PagedResponse<LabIndicatorResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorDetailResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var data = await _labIndicatorService.GetLabIndicatorByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<LabIndicatorDetailResponse>
            {
                Success = false,
                Message = "Lab indicator not found.",
            });
        }

        return Ok(new ApiResponse<LabIndicatorDetailResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLabIndicatorRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _labIndicatorService.CreateLabIndicatorAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<LabIndicatorResponse>
            {
                Success = false,
                Message = "Create lab indicator failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<LabIndicatorResponse>
        {
            Success = true,
            Message = "Lab indicator created.",
            Data = data,
        });
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkCreate(
        [FromBody] BulkCreateLabIndicatorsRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _labIndicatorService.BulkCreateLabIndicatorsAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorResponse>>
            {
                Success = false,
                Message = "Bulk create lab indicators failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<LabIndicatorResponse>>
        {
            Success = true,
            Message = $"{data.Count} lab indicator(s) created.",
            Data = data,
        });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLabIndicatorRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateLabIndicatorAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<LabIndicatorResponse>
            {
                Success = false,
                Message = "Lab indicator not found.",
            });
        }

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<LabIndicatorResponse>
            {
                Success = false,
                Message = "Update lab indicator failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<LabIndicatorResponse>
        {
            Success = true,
            Message = "Lab indicator updated.",
            Data = data,
        });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteLabIndicatorAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = "Lab indicator not found.",
            });
        }

        if (!ok)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Delete lab indicator failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Lab indicator deleted (soft).",
        });
    }

    [HttpPost("{id:guid}/aliases/bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateAliases(
        Guid id,
        [FromBody] BulkCreateLabIndicatorAliasesRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.BulkCreateAliasesAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>
            {
                Success = false,
                Message = "Lab indicator not found.",
            });
        }

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>
            {
                Success = false,
                Message = "Bulk create aliases failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>
        {
            Success = true,
            Message = $"{data.Count} alias(es) created.",
            Data = data,
        });
    }

    [HttpPost("{id:guid}/reference-ranges/bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateReferenceRanges(
        Guid id,
        [FromBody] BulkCreateLabIndicatorReferenceRangesRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.BulkCreateReferenceRangesAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>
            {
                Success = false,
                Message = "Lab indicator not found.",
            });
        }

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>
            {
                Success = false,
                Message = "Bulk create reference ranges failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>
        {
            Success = true,
            Message = $"{data.Count} reference range(s) created.",
            Data = data,
        });
    }

    [HttpPost("{id:guid}/advice/bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateAdviceCaches(
        Guid id,
        [FromBody] BulkCreateLabIndicatorAdviceCachesRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.BulkCreateAdviceCachesAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>
            {
                Success = false,
                Message = "Lab indicator not found.",
            });
        }

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>
            {
                Success = false,
                Message = "Bulk create advice caches failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>
        {
            Success = true,
            Message = $"{data.Count} advice cache entry(ies) created.",
            Data = data,
        });
    }

    [HttpPost("{id:guid}/aliases")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAlias(
        Guid id,
        [FromBody] CreateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorAliasResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.CreateAliasAsync(id, request, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "Alias created.", "Create alias failed.", "Lab indicator not found.");
    }

    [HttpPut("{id:guid}/aliases/{aliasId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAlias(
        Guid id,
        Guid aliasId,
        [FromBody] UpdateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorAliasResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateAliasAsync(id, aliasId, request, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "Alias updated.", "Update alias failed.", "Alias not found.");
    }

    [HttpDelete("{id:guid}/aliases/{aliasId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteAlias(Guid id, Guid aliasId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteAliasAsync(id, aliasId, cancellationToken);
        return ToChildDeleteResult(ok, notFound, errors, "Alias deleted (soft).", "Delete alias failed.", "Alias not found.");
    }

    [HttpPost("{id:guid}/reference-ranges")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReferenceRange(
        Guid id,
        [FromBody] CreateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorReferenceRangeResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.CreateReferenceRangeAsync(id, request, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "Reference range created.", "Create reference range failed.", "Lab indicator not found.");
    }

    [HttpPut("{id:guid}/reference-ranges/{rangeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReferenceRange(
        Guid id,
        Guid rangeId,
        [FromBody] UpdateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorReferenceRangeResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateReferenceRangeAsync(id, rangeId, request, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "Reference range updated.", "Update reference range failed.", "Reference range not found.");
    }

    [HttpDelete("{id:guid}/reference-ranges/{rangeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteReferenceRange(Guid id, Guid rangeId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteReferenceRangeAsync(id, rangeId, cancellationToken);
        return ToChildDeleteResult(ok, notFound, errors, "Reference range deleted (soft).", "Delete reference range failed.", "Reference range not found.");
    }

    [HttpPost("{id:guid}/advice")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAdviceCache(
        Guid id,
        [FromBody] CreateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorAdviceCacheResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.CreateAdviceCacheAsync(id, request, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "Advice cache created.", "Create advice cache failed.", "Lab indicator not found.");
    }

    [HttpPut("{id:guid}/advice/{cacheId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAdviceCache(
        Guid id,
        Guid cacheId,
        [FromBody] UpdateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<LabIndicatorAdviceCacheResponse>
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateAdviceCacheAsync(id, cacheId, request, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "Advice cache updated.", "Update advice cache failed.", "Advice cache not found.");
    }

    [HttpDelete("{id:guid}/advice/{cacheId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteAdviceCache(Guid id, Guid cacheId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Invalid lab indicator id.",
            });
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteAdviceCacheAsync(id, cacheId, cancellationToken);
        return ToChildDeleteResult(ok, notFound, errors, "Advice cache deleted (soft).", "Delete advice cache failed.", "Advice cache not found.");
    }

    private IActionResult ToChildMutationResult<T>(
        bool ok,
        bool notFound,
        IEnumerable<string> errors,
        T? data,
        string successMessage,
        string failureMessage,
        string notFoundMessage)
    {
        if (notFound)
        {
            return NotFound(new ApiResponse<T>
            {
                Success = false,
                Message = notFoundMessage,
            });
        }

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<T>
            {
                Success = false,
                Message = failureMessage,
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<T>
        {
            Success = true,
            Message = successMessage,
            Data = data,
        });
    }

    private IActionResult ToChildDeleteResult(
        bool ok,
        bool notFound,
        IEnumerable<string> errors,
        string successMessage,
        string failureMessage,
        string notFoundMessage)
    {
        if (notFound)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = notFoundMessage,
            });
        }

        if (!ok)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = failureMessage,
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = successMessage,
        });
    }
}
