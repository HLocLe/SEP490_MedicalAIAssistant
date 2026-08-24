using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class SymptomAnalysisQuotaService
    : ServiceCreditSessionQuotaService<SymptomAnalysisSession>,
      ISymptomAnalysisQuotaService
{
    public SymptomAnalysisQuotaService(
        IServiceCreditService serviceCreditService,
        IGenericRepository<SymptomAnalysisSession> sessions,
        IUnitOfWork unitOfWork,
        ILogger<SymptomAnalysisQuotaService> logger)
        : base(
            serviceCreditService,
            sessions,
            unitOfWork,
            logger,
            "SymptomAnalysisSession",
            "symptom-analysis",
            "Symptom analysis",
            static session => session.UserId ?? Guid.Empty,
            static session => session.UserSubscriptionId,
            static session => session.UserSubscriptionUsageId,
            static session => session.Status switch
            {
                SymptomAnalysisSessionStatus.Completed => SubscriptionQuotaActionType.Consume,
                SymptomAnalysisSessionStatus.Failed => SubscriptionQuotaActionType.Release,
                _ => null
            })
    {
    }
}
