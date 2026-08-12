using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class UserSubscriptionUsageConfiguration : IEntityTypeConfiguration<UserSubscriptionUsage>
{
    public void Configure(EntityTypeBuilder<UserSubscriptionUsage> builder)
    {
        builder.ToTable("UserSubscriptionUsage", t =>
        {
            t.HasCheckConstraint("CK_UserSubscriptionUsage_LimitValue", "\"LimitValue\" >= 0");
            t.HasCheckConstraint("CK_UserSubscriptionUsage_UsedCount", "\"UsedCount\" >= 0");
            t.HasCheckConstraint("CK_UserSubscriptionUsage_ReservedCount", "\"ReservedCount\" >= 0");
            t.HasCheckConstraint("CK_UserSubscriptionUsage_Total", "\"UsedCount\" + \"ReservedCount\" <= \"LimitValue\"");
            t.HasCheckConstraint(
                "CK_UserSubscriptionUsage_Cycle",
                "\"CycleEnd\" IS NULL OR \"CycleEnd\" > \"CycleStart\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserSubscriptionUsageId").ValueGeneratedOnAdd();
        builder.Property(x => x.Version).HasDefaultValue(0).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserSubscriptionId, x.QuotaId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.UserSubscriptionId, x.CycleEnd });
        builder.HasOne(x => x.UserSubscription).WithMany(x => x.Usages).HasForeignKey(x => x.UserSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Quota).WithMany(x => x.UserSubscriptionUsages).HasForeignKey(x => x.QuotaId).OnDelete(DeleteBehavior.Restrict);
    }
}
