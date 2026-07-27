using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class SubscriptionPlanQuotaConfiguration : IEntityTypeConfiguration<SubscriptionPlanQuota>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlanQuota> builder)
    {
        builder.ToTable("SubscriptionPlanQuota", t => t.HasCheckConstraint("CK_SubscriptionPlanQuota_LimitValue", "\"LimitValue\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("SubscriptionPlanQuotaId").ValueGeneratedOnAdd();
        builder.Property(x => x.ResetPeriod).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.PlanId, x.QuotaId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasOne(x => x.Plan).WithMany(x => x.SubscriptionPlanQuotas).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Quota).WithMany(x => x.SubscriptionPlanQuotas).HasForeignKey(x => x.QuotaId).OnDelete(DeleteBehavior.Restrict);
    }
}
