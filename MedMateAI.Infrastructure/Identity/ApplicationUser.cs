using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace MedMateAI.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public PatientProfile? PatientProfile { get; set; }

    public Doctor? Doctor { get; set; }

    public ICollection<SymptomAnalysisSession> SymptomAnalysisSessions { get; set; } = new List<SymptomAnalysisSession>();

    public ICollection<ConsultationSession> ConsultationSessions { get; set; } = new List<ConsultationSession>();

    public ICollection<FeedbackReview> FeedbackReviews { get; set; } = new List<FeedbackReview>();

    public ICollection<AIAnalysis> AIAnalyses { get; set; } = new List<AIAnalysis>();

    public ICollection<LabTestSession> LabTestSessions { get; set; } = new List<LabTestSession>();

    public ICollection<UserMedication> UserMedications { get; set; } = new List<UserMedication>();

    public ICollection<TreatmentJourney> TreatmentJourneys { get; set; } = new List<TreatmentJourney>();

    public ICollection<FollowUpReminder> FollowUpReminders { get; set; } = new List<FollowUpReminder>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public ICollection<UserPushDevice> PushDevices { get; set; } = new List<UserPushDevice>();

    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();

    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public string? DisplayName { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public UserStatus Status { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public bool IsFirstLogin { get; set; }

    public bool IsProfileCompleted { get; set; }
}
