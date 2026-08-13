using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class ConsultationSessionQuotaService
    : ServiceCreditSessionQuotaService<ConsultationSession>,
      IConsultationSessionQuotaService
{
    public ConsultationSessionQuotaService(
        IServiceCreditService serviceCreditService,
        IGenericRepository<ConsultationSession> sessions,
        IUnitOfWork unitOfWork,
        ILogger<ConsultationSessionQuotaService> logger)
        : base(
            serviceCreditService,
            sessions,
            unitOfWork,
            logger,
            "ConsultationSession",
            "consultation-session",
            "Consultation",
            static session => session.UserId,
            static session => session.UserSubscriptionId,
            static session => session.UserSubscriptionUsageId,
            static session => session.Status switch
            {
                ConsultationSessionStatus.Completed => SubscriptionQuotaActionType.Consume,
                ConsultationSessionStatus.Failed => SubscriptionQuotaActionType.Release,
                _ => null
            })
    {
    }
}
