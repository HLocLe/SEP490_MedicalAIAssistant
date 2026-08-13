using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabTestSessionConfiguration : IEntityTypeConfiguration<LabTestSession>
{
    public void Configure(EntityTypeBuilder<LabTestSession> builder)
    {
        builder.ToTable("LabTestSession", table =>
            table.HasCheckConstraint(
                "CK_LabTestSession_ServiceCreditLinkage",
                "(\"UserSubscriptionId\" IS NULL AND \"UserSubscriptionUsageId\" IS NULL) OR "
                + "(\"UserSubscriptionId\" IS NOT NULL AND \"UserSubscriptionUsageId\" IS NOT NULL)"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TestSessionId").ValueGeneratedOnAdd();

        builder.Property(x => x.DocumentUrl).HasMaxLength(2048);
        builder.Property(x => x.FacilityName).HasMaxLength(255);

        builder.HasIndex(x => x.UserSubscriptionId);
        builder.HasIndex(x => x.UserSubscriptionUsageId);

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.LabTestSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserSubscription)
            .WithMany()
            .HasForeignKey(x => x.UserSubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserSubscriptionUsage)
            .WithMany()
            .HasForeignKey(x => x.UserSubscriptionUsageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LabTestOcrExtracts)
            .WithOne(x => x.TestSession)
            .HasForeignKey(x => x.TestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.LabTestResultDetails)
            .WithOne(x => x.TestSession)
            .HasForeignKey(x => x.TestSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
