using MedMateAI.Application.IService;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public sealed class ConsultationDoctorQuestionsJob
{
    private readonly IConsultationSessionService _consultationSessionService;
    private readonly IConsultationSessionQuotaService _quotaService;

    public ConsultationDoctorQuestionsJob(
        IConsultationSessionService consultationSessionService,
        IConsultationSessionQuotaService quotaService)
    {
        _consultationSessionService = consultationSessionService;
        _quotaService = quotaService;
    }

    public async Task ExecuteAsync(Guid sessionId)
    {
        try
        {
            await _consultationSessionService.ProcessGenerateDoctorQuestionsAsync(sessionId);
        }
        finally
        {
            await _quotaService.FinalizeAsync(sessionId);
        }
    }
}
