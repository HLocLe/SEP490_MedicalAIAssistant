using MedMateAI.Application.DTOs.Common;

using MedMateAI.Application.DTOs.ConsultationSessions.Requests;

using MedMateAI.Application.DTOs.ConsultationSessions.Responses;

using MedMateAI.Application.IService;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;



namespace MedMateAI.Controllers;



[ApiController]

[Route("api/consultation-sessions")]

public sealed class ConsultationSessionController : ControllerBase

{




    private readonly IConsultationSessionService _consultationSessionService;

    private readonly IUserService _userService;



    public ConsultationSessionController(

        IConsultationSessionService consultationSessionService,

        IUserService userService)

    {

        _consultationSessionService = consultationSessionService;

        _userService = userService;

    }



    [Authorize]

    [HttpPost("generate-questions-for-consultant-session")]

    [ProducesResponseType(typeof(ApiResponse<GenerateConsultationQuestionsResponse>), StatusCodes.Status200OK)]

    [ProducesResponseType(typeof(ApiResponse<GenerateConsultationQuestionsResponse>), StatusCodes.Status400BadRequest)]

    [ProducesResponseType(typeof(ApiResponse<GenerateConsultationQuestionsResponse>), StatusCodes.Status401Unauthorized)]

    public async Task<IActionResult> GenerateDoctorQuestions(

        [FromBody] GenerateConsultationQuestionsRequest request,

        CancellationToken cancellationToken = default)

    {

        var currentUser = await _userService.GetCurrentUserAsync(cancellationToken);

        if (currentUser is null)

        {

            return Unauthorized(new ApiResponse<GenerateConsultationQuestionsResponse>

            {

                Success = false,

                Message = "Unauthorized",

            });

        }



        if (request is null || request.DepartmentId == Guid.Empty)

        {

            return BadRequest(new ApiResponse<GenerateConsultationQuestionsResponse>

            {

                Success = false,

                Message = "Generate doctor questions failed.",

                Errors = new List<string> { "Department id is required." },

            });

        }



        if (string.IsNullOrWhiteSpace(request.Symptoms))

        {

            return BadRequest(new ApiResponse<GenerateConsultationQuestionsResponse>

            {

                Success = false,

                Message = "Generate doctor questions failed.",

                Errors = new List<string> { "Symptoms are required." },

            });

        }



       



        var (ok, errors, data) = await _consultationSessionService.GenerateDoctorQuestionsAsync(

            currentUser.Id,

            request.DepartmentId,

            request.Symptoms,

            cancellationToken);



        if (!ok || data is null)

        {

            return BadRequest(new ApiResponse<GenerateConsultationQuestionsResponse>

            {

                Success = false,

                Message = "Generate doctor questions failed.",

                Errors = errors.ToList(),

            });

        }



        return Ok(new ApiResponse<GenerateConsultationQuestionsResponse>

        {

            Success = true,

            Message = "OK",

            Data = data,

        });

    }



    [Authorize]

    [HttpGet("my-sessions")]

    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ConsultationSessionSummaryResponse>>), StatusCodes.Status200OK)]

    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ConsultationSessionSummaryResponse>>), StatusCodes.Status401Unauthorized)]

    public async Task<IActionResult> GetMySessions(

        [FromQuery] PaginationQuery query,

        CancellationToken cancellationToken = default)

    {

        var currentUser = await _userService.GetCurrentUserAsync(cancellationToken);

        if (currentUser is null)

        {

            return Unauthorized(new ApiResponse<PagedResponse<ConsultationSessionSummaryResponse>>

            {

                Success = false,

                Message = "Unauthorized",

            });

        }



        var data = await _consultationSessionService.GetMyCompletedSessionsAsync(

            currentUser.Id,

            query.PageNumber,

            query.PageSize,

            cancellationToken);



        return Ok(new ApiResponse<PagedResponse<ConsultationSessionSummaryResponse>>

        {

            Success = true,

            Message = "OK",

            Data = data,

        });

    }



    [Authorize]

    [HttpGet("{sessionId:guid}")]

    [ProducesResponseType(typeof(ApiResponse<ConsultationSessionDetailResponse>), StatusCodes.Status200OK)]

    [ProducesResponseType(typeof(ApiResponse<ConsultationSessionDetailResponse>), StatusCodes.Status401Unauthorized)]

    [ProducesResponseType(typeof(ApiResponse<ConsultationSessionDetailResponse>), StatusCodes.Status404NotFound)]

    public async Task<IActionResult> GetById(

        Guid sessionId,

        CancellationToken cancellationToken = default)

    {

        var currentUser = await _userService.GetCurrentUserAsync(cancellationToken);

        if (currentUser is null)

        {

            return Unauthorized(new ApiResponse<ConsultationSessionDetailResponse>

            {

                Success = false,

                Message = "Unauthorized",

            });

        }



        var (notFound, data) = await _consultationSessionService.GetConsultationSessionByIdAsync(

            currentUser.Id,

            sessionId,

            cancellationToken);



        if (notFound || data is null)

        {

            return NotFound(new ApiResponse<ConsultationSessionDetailResponse>

            {

                Success = false,

                Message = "Consultation session not found.",

            });

        }



        return Ok(new ApiResponse<ConsultationSessionDetailResponse>

        {

            Success = true,

            Message = "OK",

            Data = data,

        });

    }

}


