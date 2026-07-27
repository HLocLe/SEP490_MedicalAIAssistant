using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class UserSubscriptionLogConfiguration : IEntityTypeConfiguration<UserSubscriptionLog>
{
    public void Configure(EntityTypeBuilder<UserSubscriptionLog> builder)
    {
        builder.ToTable("UserSubscriptionLog", t =>
        {
            t.HasCheckConstraint("CK_UserSubscriptionLog_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_UserSubscriptionLog_Counts", "\"UsedCountBefore\" >= 0 AND \"UsedCountAfter\" >= 0 AND \"ReservedCountBefore\" >= 0 AND \"ReservedCountAfter\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserSubscriptionLogId").ValueGeneratedOnAdd();
        builder.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.UserSubscriptionUsageId, x.CreatedAt });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasOne(x => x.UserSubscription).WithMany(x => x.QuotaLogs).HasForeignKey(x => x.UserSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UserSubscriptionUsage).WithMany(x => x.Logs).HasForeignKey(x => x.UserSubscriptionUsageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Quota).WithMany(x => x.UserSubscriptionLogs).HasForeignKey(x => x.QuotaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
