using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Domain.Persistence;

public interface IUnitOfWork : IAsyncDisposable
{
    IMedicalFacilityRepository MedicalFacilities { get; }
    IDoctorRepository Doctors { get; }
    IDoctorInvitationRepository DoctorInvitations { get; }
    IFeedbackReviewRepository FeedbackReviews { get; }
    IGenericRepository<SubscriptionPlan> SubscriptionPlans { get; }
    IUserSubscriptionRepository UserSubscriptions { get; }
    IPaymentRepository Payments { get; }
    IPaymentTransactionRepository PaymentTransactions { get; }

    ISymptomAnalysisSessionRepository SymptomAnalysisSessions { get; }

    ISessionSymptomRepository SessionSymptoms { get; }

    IDepartmentRecommendationRepository DepartmentRecommendations { get; }

    IMedicalDepartmentRepository MedicalDepartments { get; }

    IFacilityDepartmentRepository FacilityDepartments { get; }

    IClinicalQuestionRepository ClinicalQuestions { get; }

    ISessionClinicalQuestionAnswerRepository SessionClinicalQuestionAnswers { get; }

    IIcdChapterRepository IcdChapters { get; }
    IRecoveryPlanRequestRepository RecoveryPlanRequests { get; }
    IQuotaUsageRepository QuotaUsages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
