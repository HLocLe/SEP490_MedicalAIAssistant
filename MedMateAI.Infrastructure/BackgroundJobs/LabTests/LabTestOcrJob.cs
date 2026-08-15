using MedMateAI.Application.IService;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public sealed class LabTestOcrJob
{
    private readonly ILabTestOcrProcessor _processor;
    private readonly ILabTestQuotaService _quotaService;

    public LabTestOcrJob(
        ILabTestOcrProcessor processor,
        ILabTestQuotaService quotaService)
    {
        _processor = processor;
        _quotaService = quotaService;
    }

    public async Task ExecuteAsync(Guid sessionId)
    {
        try
        {
            await _processor.ProcessAsync(sessionId);
        }
        finally
        {
            await _quotaService.FinalizeAsync(sessionId);
        }
    }
}
