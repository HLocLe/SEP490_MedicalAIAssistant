using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.SymptomAnalysis.Requests;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.Session;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/symptom-analysis")]
public sealed class SymptomAnalysisController : ControllerBase
{
    private const int MaxMessageLength = 2000;

    private readonly ISymptomAnalysisService _symptomAnalysisService;
    private readonly IUserService _userService;

    public SymptomAnalysisController(
        ISymptomAnalysisService symptomAnalysisService,
        IUserService userService)
    {
        _symptomAnalysisService = symptomAnalysisService;
        _userService = userService;
    }

    [HttpPost("suggest-clinical-questions")]
    [ProducesResponseType(typeof(ApiResponse<SuggestClinicalQuestionsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SuggestClinicalQuestionsResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SuggestClinicalQuestions(
        [FromBody] SuggestClinicalQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserInput))
        {
            return BadRequest(new ApiResponse<SuggestClinicalQuestionsResponse>
            {
                Success = false,
                Message = "Suggest clinical questions failed.",
                Errors = new List<string> { "User input is required." },
            });
        }

        if (request.UserInput.Trim().Length > MaxMessageLength)
        {
            return BadRequest(new ApiResponse<SuggestClinicalQuestionsResponse>
            {
                Success = false,
                Message = "Suggest clinical questions failed.",
                Errors = new List<string> { $"User input must be {MaxMessageLength} characters or fewer." },
            });
        }

        try
        {
            var data = await _symptomAnalysisService.SuggestClinicalQuestionAsync(request, cancellationToken);
            return Ok(new ApiResponse<SuggestClinicalQuestionsResponse>
            {
                Success = true,
                Message = "OK",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<SuggestClinicalQuestionsResponse>
            {
                Success = false,
                Message = "Suggest clinical questions failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [HttpPost("submit-clinical-question-answers")]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionAnswersResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionAnswersResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalQuestionAnswersResponse>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SubmitClinicalQuestionAnswers(
        [FromBody] SubmitClinicalQuestionAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.SessionId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<ClinicalQuestionAnswersResponse>
            {
                Success = false,
                Message = "Submit clinical question answers failed.",
                Errors = new List<string> { "Session id is required." },
            });
        }

        try
        {
            var data = await _symptomAnalysisService.SubmitClinicalQuestionAnswersAsync(request, cancellationToken);
            return Ok(new ApiResponse<ClinicalQuestionAnswersResponse>
            {
                Success = true,
                Message = "OK",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<ClinicalQuestionAnswersResponse>
            {
                Success = false,
                Message = "Submit clinical question answers failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ApiResponse<ClinicalQuestionAnswersResponse>
            {
                Success = false,
                Message = "MedGemma analysis failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [HttpPost("submit-diagnosis")]
    [ProducesResponseType(typeof(ApiResponse<DiagnosisSubmitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DiagnosisSubmitResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DiagnosisSubmitResponse>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SubmitDiagnosis(
        [FromBody] SubmitClinicalQuestionAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.SessionId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<DiagnosisSubmitResponse>
            {
                Success = false,
                Message = "Submit diagnosis failed.",
                Errors = new List<string> { "Session id is required." },
            });
        }

        try
        {
            var data = await _symptomAnalysisService.SubmitDiagnosisAsync(request, cancellationToken);
            return Ok(new ApiResponse<DiagnosisSubmitResponse>
            {
                Success = true,
                Message = "OK",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<DiagnosisSubmitResponse>
            {
                Success = false,
                Message = "Submit diagnosis failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ApiResponse<DiagnosisSubmitResponse>
            {
                Success = false,
                Message = "MedGemma analysis failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [HttpGet("my-sessions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<SymptomAnalysisSessionSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<SymptomAnalysisSessionSummaryResponse>>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySessions(
        [FromQuery] PaginationQuery query,
        [FromQuery] SymptomAnalysisSessionType? sessionType,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await _userService.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized(new ApiResponse<PagedResponse<SymptomAnalysisSessionSummaryResponse>>
            {
                Success = false,
                Message = "Unauthorized",
            });
        }

        var data = await _symptomAnalysisService.GetSessionsByUserIdAsync(
            currentUser.Id,
            sessionType,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Ok(new ApiResponse<PagedResponse<SymptomAnalysisSessionSummaryResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(ApiResponse<SymptomAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SymptomAnalysisResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var data = await _symptomAnalysisService.GetSessionByIdAsync(sessionId, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<SymptomAnalysisResponse>
            {
                Success = false,
                Message = "Symptom analysis session not found.",
            });
        }

        return Ok(new ApiResponse<SymptomAnalysisResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }
}
