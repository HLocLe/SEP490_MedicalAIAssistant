using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanRequestConfiguration : IEntityTypeConfiguration<RecoveryPlanRequest>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanRequest> builder)
    {
        builder.ToTable("RecoveryPlanRequest");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanRequestId").ValueGeneratedOnAdd();
        builder.Property(x => x.DiseaseGroup).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.RequestNote).HasMaxLength(2000);
        builder.Property(x => x.RejectionReasonCode).HasMaxLength(100);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.Version).HasDefaultValue(0).IsConcurrencyToken();
        builder.HasIndex(x => new { x.Status, x.RequestedAt });
        builder.HasIndex(x => x.AssignedDoctorId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserSubscriptionUsageId, x.Status });
        builder.HasIndex(x => x.AssignmentExpiresAt);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedDoctor).WithMany(x => x.AssignedRecoveryPlanRequests).HasForeignKey(x => x.AssignedDoctorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.TreatmentJourney).WithMany().HasForeignKey(x => x.TreatmentJourneyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.PrimaryLabTestSession).WithMany().HasForeignKey(x => x.PrimaryLabTestSessionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.UserSubscription).WithMany(x => x.RecoveryPlanRequests).HasForeignKey(x => x.UserSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UserSubscriptionUsage).WithMany(x => x.RecoveryPlanRequests).HasForeignKey(x => x.UserSubscriptionUsageId).OnDelete(DeleteBehavior.Restrict);
    }
}
