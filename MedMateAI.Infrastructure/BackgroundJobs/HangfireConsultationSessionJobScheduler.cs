using Hangfire;
using MedMateAI.Application.IService;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public sealed class HangfireConsultationSessionJobScheduler : IConsultationSessionJobScheduler
{
    public void EnqueueGenerateDoctorQuestions(Guid sessionId)
    {
        BackgroundJob.Enqueue<ConsultationDoctorQuestionsJob>(job => job.ExecuteAsync(sessionId));
    }
}
