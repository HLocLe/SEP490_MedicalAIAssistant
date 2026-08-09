namespace MedMateAI.Application.IService;

public interface IConsultationSessionJobScheduler
{
    void EnqueueGenerateDoctorQuestions(Guid sessionId);
}
