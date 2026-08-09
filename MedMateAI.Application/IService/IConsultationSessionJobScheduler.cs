namespace MedMateAI.Application.IService;

public interface IConsultationSessionJobScheduler
{
    void EnqueueGenerateDoctorQuestions(Guid sessionId);

    void EnqueueReminderSms(Guid sessionId);

    void ScheduleReminderSms(Guid sessionId, DateTime enqueueAtUtc);
}
