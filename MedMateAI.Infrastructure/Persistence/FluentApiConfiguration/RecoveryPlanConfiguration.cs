using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanConfiguration : IEntityTypeConfiguration<RecoveryPlan>
{
    public void Configure(EntityTypeBuilder<RecoveryPlan> builder)
    {
        builder.ToTable("RecoveryPlan", table =>
            table.HasCheckConstraint(
                "CK_RecoveryPlan_FeedbackRating",
                "\"FeedbackRating\" IS NULL OR (\"FeedbackRating\" >= 1 AND \"FeedbackRating\" <= 5)"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanId").ValueGeneratedOnAdd();
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).HasDefaultValue(RecoveryPlanStatus.Draft).IsRequired();
        builder.Property(x => x.RecheckInstruction).HasMaxLength(2000);
        builder.Property(x => x.CancellationReasonCode).HasMaxLength(100);
        builder.Property(x => x.CancellationReason).HasMaxLength(2000);
        builder.Property(x => x.FeedbackNote).HasMaxLength(1000);
        builder.Property(x => x.ClinicalSnapshotJson).HasColumnType("jsonb");
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.HasIndex(x => x.RecoveryPlanRequestId).IsUnique().HasFilter("\"RecoveryPlanRequestId\" IS NOT NULL AND \"IsDeleted\" = false");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecoveryPlanRequest).WithOne(x => x.RecoveryPlan).HasForeignKey<RecoveryPlan>(x => x.RecoveryPlanRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Doctor).WithMany(x => x.RecoveryPlans).HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.TreatmentJourney).WithMany(x => x.RecoveryPlans).HasForeignKey(x => x.TreatmentJourneyId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TestSession)
            .WithMany(x => x.RecoveryPlans)
            .HasForeignKey(x => x.TestSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SymptomAnalysisSession)
            .WithMany(x => x.RecoveryPlans)
            .HasForeignKey(x => x.SymptomAnalysisSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.TreatmentLogs)
            .WithOne(x => x.RecoveryPlan)
            .HasForeignKey(x => x.RecoveryPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
