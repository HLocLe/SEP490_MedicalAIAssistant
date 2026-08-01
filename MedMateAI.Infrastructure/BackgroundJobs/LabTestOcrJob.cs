using MedMateAI.Application.IService;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public sealed class LabTestOcrJob
{
    private readonly ILabTestOcrProcessor _processor;

    public LabTestOcrJob(ILabTestOcrProcessor processor)
    {
        _processor = processor;
    }

    public Task ExecuteAsync(Guid sessionId)
    {
        return _processor.ProcessAsync(sessionId);
    }
}
