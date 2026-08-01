namespace MedMateAI.Application.IService;

public interface ILabTestJobScheduler
{
    void EnqueueOcr(Guid sessionId);
}
