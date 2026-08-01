using System.Text.Json;

using MedMateAI.Application.DTOs.Common;

using MedMateAI.Application.DTOs.ConsultationSessions.Responses;

using MedMateAI.Application.DTOs.PatientProfiles.Responses;

using MedMateAI.Application.DTOs.WebChatbot.Requests;

using MedMateAI.Application.DTOs.WebChatbot.Responses;

using MedMateAI.Application.IService;

using MedMateAI.Domain.Entities;

using MedMateAI.Domain.Enums;

using MedMateAI.Domain.Persistence;

using MedMateAI.Domain.Repository;



namespace MedMateAI.Application.Service;



public sealed class ConsultationSessionService : IConsultationSessionService

{

    private const string DoctorQuestionsTaskType = "ConsultationDoctorQuestions";



    private static readonly JsonSerializerOptions QuestionJsonOptions = new()

    {

        PropertyNameCaseInsensitive = true,

    };



    private readonly IMedicalDepartmentService _medicalDepartmentService;

    private readonly IAIConfigService _aiConfigService;

    private readonly IAIChatProvider _aiChatProvider;

    private readonly IPatientProfileService _patientProfileService;

    private readonly IGenericRepository<ConsultationSession> _consultationSessions;

    private readonly IGenericRepository<ConsultationQuestion> _consultationQuestions;

    private readonly IUnitOfWork _unitOfWork;



    public ConsultationSessionService(

        IMedicalDepartmentService medicalDepartmentService,

        IAIConfigService aiConfigService,

        IAIChatProvider aiChatProvider,

        IPatientProfileService patientProfileService,

        IGenericRepository<ConsultationSession> consultationSessions,

        IGenericRepository<ConsultationQuestion> consultationQuestions,

        IUnitOfWork unitOfWork)

    {

        _medicalDepartmentService = medicalDepartmentService;

        _aiConfigService = aiConfigService;

        _aiChatProvider = aiChatProvider;

        _patientProfileService = patientProfileService;

        _consultationSessions = consultationSessions;

        _consultationQuestions = consultationQuestions;

        _unitOfWork = unitOfWork;

    }



    public async Task<(bool Succeeded, IEnumerable<string> Errors, GenerateConsultationQuestionsResponse? Data)> GenerateDoctorQuestionsAsync(

        Guid userId,

        Guid departmentId,

        string symptoms,

        CancellationToken cancellationToken = default)

    {

        if (userId == Guid.Empty)

        {

            return (false, new[] { "User id is required." }, null);

        }



        if (departmentId == Guid.Empty)

        {

            return (false, new[] { "Department id is required." }, null);

        }



        if (string.IsNullOrWhiteSpace(symptoms))

        {

            return (false, new[] { "Symptoms are required." }, null);

        }



        var department = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(departmentId, cancellationToken);

        if (department is null)

        {

            return (false, new[] { "Department not found." }, null);

        }



        var departmentName = department.DepartmentName?.Trim();

        if (string.IsNullOrWhiteSpace(departmentName))

        {

            return (false, new[] { "Department name is not available." }, null);

        }



        var aiConfig = await _aiConfigService.GetActiveAIConfigByTaskTypeAsync(

            DoctorQuestionsTaskType,

            cancellationToken);



        if (aiConfig is null || string.IsNullOrWhiteSpace(aiConfig.SystemPrompt))

        {

            return (false, new[] { "AI is not config." }, null);

        }



        var utcNow = DateTime.UtcNow;

        var trimmedSymptoms = symptoms.Trim();

        var session = new ConsultationSession

        {

            Id = Guid.NewGuid(),

            UserId = userId,

            DepartmentId = departmentId,

            UserSymptoms = trimmedSymptoms,

            Status = ConsultationSessionStatus.Processing,

            CreatedAt = utcNow,

        };



        _consultationSessions.Add(session);

        await _unitOfWork.SaveChangesAsync(cancellationToken);



        var (_, patientProfile) = await _patientProfileService.GetPatientProfileByUserIdAsync(

            userId,

            cancellationToken);

        var chronicDiseases = patientProfile?.ChronicDiseases ?? Array.Empty<PatientChronicDiseaseResponse>();



        var userPrompt = BuildUserPrompt(departmentName, trimmedSymptoms, chronicDiseases);



        AIProviderChatResult aiResult;

        try

        {

            aiResult = await _aiChatProvider.GenerateAsync(

                new AIProviderChatRequest

                {

                    SystemPrompt = aiConfig.SystemPrompt.Trim(),

                    UserMessage = userPrompt,

                    Model = aiConfig.Model ?? string.Empty,

                    Temperature = aiConfig.Temperature,

                    MaxTokens = aiConfig.MaxTokens,

                },

                cancellationToken);

        }

        catch (InvalidOperationException ex)

        {

            await MarkSessionFailedAsync(session, cancellationToken);

            return (false, new[] { ex.Message }, null);

        }



        if (!TryParseDoctorQuestionsJson(aiResult.Content, out var questions))

        {

            await MarkSessionFailedAsync(session, cancellationToken);

            return (false, new[] { "Failed to parse doctor questions from AI response." }, null);

        }



        var priority = 0;

        foreach (var question in questions)

        {

            _consultationQuestions.Add(new ConsultationQuestion

            {

                Id = Guid.NewGuid(),

                ConsultationSessionId = session.Id,

                Category = question.Category,

                QuestionText = question.Question,

                Priority = priority++,

                CreatedAt = utcNow,

            });

        }



        session.Status = ConsultationSessionStatus.Completed;

        session.UpdatedAt = utcNow;

        _consultationSessions.Update(session);

        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return (true, Array.Empty<string>(), new GenerateConsultationQuestionsResponse

        {

            SessionId = session.Id,

            DepartmentId = departmentId,

            DepartmentName = departmentName,

            Symptoms = trimmedSymptoms,

            Status = session.Status,

            Questions = questions,

            Model = aiResult.Model,

        });

    }



    public async Task<PagedResponse<ConsultationSessionSummaryResponse>> GetMyCompletedSessionsAsync(

        Guid userId,

        int pageNumber,

        int pageSize,

        CancellationToken cancellationToken = default)

    {

        if (userId == Guid.Empty)

        {

            return new PagedResponse<ConsultationSessionSummaryResponse>();

        }



        var paged = await _consultationSessions.GetPagedAsync(

            pageNumber,

            pageSize,

            x => !x.IsDeleted && x.UserId == userId && x.Status == ConsultationSessionStatus.Completed,

            q => q.OrderByDescending(x => x.CreatedAt),

            cancellationToken: cancellationToken);



        var departmentNames = new Dictionary<Guid, string>();

        foreach (var departmentId in paged.Items.Select(session => session.DepartmentId).Distinct())

        {

            var department = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(

                departmentId,

                cancellationToken);

            departmentNames[departmentId] = department?.DepartmentName?.Trim() ?? string.Empty;

        }



        return new PagedResponse<ConsultationSessionSummaryResponse>

        {

            PageNumber = paged.PageNumber,

            PageSize = paged.PageSize,

            TotalCount = paged.TotalCount,

            TotalPages = paged.TotalPages,

            Items = paged.Items

                .Select(session => new ConsultationSessionSummaryResponse

                {

                    SessionId = session.Id,

                    DepartmentId = session.DepartmentId,

                    DepartmentName = departmentNames.GetValueOrDefault(session.DepartmentId),

                    Symptoms = session.UserSymptoms?.Trim() ?? string.Empty,

                    Status = session.Status,

                    CreatedAt = session.CreatedAt,

                })

                .ToList(),

        };

    }



    public async Task<(bool NotFound, ConsultationSessionDetailResponse? Data)> GetConsultationSessionByIdAsync(

        Guid userId,

        Guid sessionId,

        CancellationToken cancellationToken = default)

    {

        if (userId == Guid.Empty || sessionId == Guid.Empty)

        {

            return (true, null);

        }



        var session = await _consultationSessions.FirstOrDefaultAsync(

            x => !x.IsDeleted && x.Id == sessionId && x.UserId == userId,

            cancellationToken: cancellationToken);



        if (session is null)

        {

            return (true, null);

        }



        var department = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(

            session.DepartmentId,

            cancellationToken);



        var questionsPaged = await _consultationQuestions.GetPagedAsync(

            1,

            100,

            q => !q.IsDeleted && q.ConsultationSessionId == sessionId,

            q => q.OrderBy(x => x.Priority),

            cancellationToken: cancellationToken);



        return (false, new ConsultationSessionDetailResponse

        {

            SessionId = session.Id,

            DepartmentId = session.DepartmentId,

            DepartmentName = department?.DepartmentName?.Trim() ?? string.Empty,

            Symptoms = session.UserSymptoms?.Trim() ?? string.Empty,

            Status = session.Status,

            CreatedAt = session.CreatedAt,

            Questions = questionsPaged.Items

                .Select(question => new ConsultationQuestionResponse

                {

                    Id = question.Id,

                    QuestionText = question.QuestionText,

                    Category = question.Category,

                    Priority = question.Priority,

                })

                .ToList(),

        });

    }



    internal static string BuildUserPrompt(

        string departmentName,

        string symptoms,

        IReadOnlyList<PatientChronicDiseaseResponse> chronicDiseases)

    {

        var lines = new List<string>

        {

            $"Department: {departmentName.Trim()}",

            $"Symptoms: {symptoms.Trim()}",

        };



        if (chronicDiseases.Count > 0)

        {

            lines.Add("Chronic diseases:");

            foreach (var disease in chronicDiseases)

            {

                if (string.IsNullOrWhiteSpace(disease.DiseaseName))

                {

                    continue;

                }



                lines.Add($"- {FormatChronicDiseaseLine(disease)}");

            }

        }



        return string.Join('\n', lines);

    }



    private static string FormatChronicDiseaseLine(PatientChronicDiseaseResponse disease)

    {

        var name = disease.DiseaseName.Trim();

        if (!disease.From.HasValue && !disease.To.HasValue)

        {

            return name;

        }



        if (disease.From.HasValue && disease.To.HasValue)

        {

            return $"{name} (from {disease.From.Value:yyyy-MM-dd} to {disease.To.Value:yyyy-MM-dd})";

        }



        if (disease.From.HasValue)

        {

            return $"{name} (from {disease.From.Value:yyyy-MM-dd})";

        }



        return $"{name} (to {disease.To!.Value:yyyy-MM-dd})";

    }



    private async Task MarkSessionFailedAsync(

        ConsultationSession session,

        CancellationToken cancellationToken)

    {

        session.Status = ConsultationSessionStatus.Failed;

        session.UpdatedAt = DateTime.UtcNow;

        _consultationSessions.Update(session);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

    }



    private static bool TryParseDoctorQuestionsJson(

        string content,

        out IReadOnlyList<ConsultationDoctorQuestionItemResponse> questions)

    {

        questions = [];



        var normalizedJson = StripMarkdownCodeFence(content);

        if (string.IsNullOrWhiteSpace(normalizedJson))

        {

            return false;

        }



        try

        {

            var parsed = JsonSerializer.Deserialize<List<ConsultationDoctorQuestionItemResponse>>(

                normalizedJson,

                QuestionJsonOptions);



            if (parsed is null || parsed.Count == 0)

            {

                return false;

            }



            questions = parsed

                .Where(item =>

                    !string.IsNullOrWhiteSpace(item.Category) &&

                    !string.IsNullOrWhiteSpace(item.Question))

                .Select(item => new ConsultationDoctorQuestionItemResponse

                {

                    Category = item.Category.Trim(),

                    Question = item.Question.Trim(),

                })

                .ToList();



            return questions.Count > 0;

        }

        catch (JsonException)

        {

            return false;

        }

    }



    private static string StripMarkdownCodeFence(string content)

    {

        var trimmed = content.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))

        {

            return trimmed;

        }



        trimmed = trimmed[3..];

        if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))

        {

            trimmed = trimmed[4..];

        }



        trimmed = trimmed.TrimStart('\r', '\n', ' ');



        var closingFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        if (closingFenceIndex >= 0)

        {

            trimmed = trimmed[..closingFenceIndex];

        }



        return trimmed.Trim();

    }

}


