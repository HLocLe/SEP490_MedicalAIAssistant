using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();

    public DbSet<PatientChronicDisease> PatientChronicDiseases => Set<PatientChronicDisease>();

    public DbSet<SymptomAnalysisSession> SymptomAnalysisSessions => Set<SymptomAnalysisSession>();

    public DbSet<SessionSymptom> SessionSymptoms => Set<SessionSymptom>();

    public DbSet<MedicalDepartment> MedicalDepartments => Set<MedicalDepartment>();

    public DbSet<DepartmentRecommendation> DepartmentRecommendations => Set<DepartmentRecommendation>();

    public DbSet<ConsultationSession> ConsultationSessions => Set<ConsultationSession>();

    public DbSet<ConsultationQuestion> ConsultationQuestions => Set<ConsultationQuestion>();

    public DbSet<MedicalFacility> MedicalFacilities => Set<MedicalFacility>();

    public DbSet<FacilityDepartment> FacilityDepartments => Set<FacilityDepartment>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<DoctorInvitation> DoctorInvitations => Set<DoctorInvitation>();

    public DbSet<FeedbackReview> FeedbackReviews => Set<FeedbackReview>();

    public DbSet<LabTestSession> LabTestSessions => Set<LabTestSession>();

    public DbSet<LabIndicatorMaster> LabIndicatorMasters => Set<LabIndicatorMaster>();

    public DbSet<LabTestResultDetail> LabTestResultDetails => Set<LabTestResultDetail>();

    public DbSet<LabIndicatorAdviceCache> LabIndicatorAdviceCaches => Set<LabIndicatorAdviceCache>();

    public DbSet<IcdChapter> IcdChapters => Set<IcdChapter>();

    public DbSet<ClinicalQuestion> ClinicalQuestions => Set<ClinicalQuestion>();

    public DbSet<SessionClinicalQuestionAnswer> SessionClinicalQuestionAnswers => Set<SessionClinicalQuestionAnswer>();

    public DbSet<AIAnalysis> AIAnalyses => Set<AIAnalysis>();

    public DbSet<AISystemConfig> AISystemConfigs => Set<AISystemConfig>();

    public DbSet<UserMedication> UserMedications => Set<UserMedication>();

    public DbSet<TreatmentJourney> TreatmentJourneys => Set<TreatmentJourney>();

    public DbSet<RecoveryPlan> RecoveryPlans => Set<RecoveryPlan>();

    public DbSet<TreatmentLog> TreatmentLogs => Set<TreatmentLog>();

    public DbSet<FollowUpReminder> FollowUpReminders => Set<FollowUpReminder>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Quota> Quotas => Set<Quota>();
    public DbSet<SubscriptionPlanQuota> SubscriptionPlanQuotas => Set<SubscriptionPlanQuota>();
    public DbSet<UserSubscriptionUsage> UserSubscriptionUsages => Set<UserSubscriptionUsage>();
    public DbSet<UserSubscriptionLog> UserSubscriptionLogs => Set<UserSubscriptionLog>();
    public DbSet<RecoveryPlanRequest> RecoveryPlanRequests => Set<RecoveryPlanRequest>();
    public DbSet<RecoveryPlanRequestEvent> RecoveryPlanRequestEvents => Set<RecoveryPlanRequestEvent>();
    public DbSet<RecoveryPlanPhase> RecoveryPlanPhases => Set<RecoveryPlanPhase>();
    public DbSet<RecoveryPlanNutrientTarget> RecoveryPlanNutrientTargets => Set<RecoveryPlanNutrientTarget>();
    public DbSet<RecoveryPlanFoodSource> RecoveryPlanFoodSources => Set<RecoveryPlanFoodSource>();
    public DbSet<UserMedicationReminderTime> UserMedicationReminderTimes => Set<UserMedicationReminderTime>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
