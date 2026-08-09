using MedMateAI.Application.IService;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public sealed class ConsultationDoctorQuestionsJob
{
    private readonly IConsultationSessionService _consultationSessionService;

    public ConsultationDoctorQuestionsJob(IConsultationSessionService consultationSessionService)
    {
        _consultationSessionService = consultationSessionService;
    }

    public Task ExecuteAsync(Guid sessionId)
    {
        return _consultationSessionService.ProcessGenerateDoctorQuestionsAsync(sessionId);
    }
}
