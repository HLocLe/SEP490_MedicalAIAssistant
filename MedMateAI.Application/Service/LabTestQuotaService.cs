using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class LabTestQuotaService
    : ServiceCreditSessionQuotaService<LabTestSession>,
      ILabTestQuotaService
{
    public LabTestQuotaService(
        IServiceCreditService serviceCreditService,
        IGenericRepository<LabTestSession> sessions,
        IUnitOfWork unitOfWork,
        ILogger<LabTestQuotaService> logger)
        : base(
            serviceCreditService,
            sessions,
            unitOfWork,
            logger,
            "LabTestSession",
            "labtest",
            "Lab test",
            static session => session.UserId,
            static session => session.UserSubscriptionId,
            static session => session.UserSubscriptionUsageId,
            static session => session.Status switch
            {
                LabTestSessionStatus.Completed => SubscriptionQuotaActionType.Consume,
                LabTestSessionStatus.Failed => SubscriptionQuotaActionType.Release,
                _ => null
            })
    {
    }
}
