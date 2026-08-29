using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class SaleRedemptionConfiguration : IEntityTypeConfiguration<SaleRedemption>
{
    public void Configure(EntityTypeBuilder<SaleRedemption> builder)
    {
        builder.ToTable("SaleRedemption", table =>
        {
            table.HasCheckConstraint(
                "CK_SaleRedemption_Prices",
                "\"OriginalPrice\" > 0 AND \"FinalPrice\" > 0 AND \"FinalPrice\" <= \"OriginalPrice\"");
            table.HasCheckConstraint(
                "CK_SaleRedemption_Credits",
                "\"BaseCredit\" > 0 AND \"BonusCredit\" >= 0 AND \"GrantedCredit\" = \"BaseCredit\" + \"BonusCredit\"");
        });

        builder.HasKey(redemption => redemption.Id);
        builder.Property(redemption => redemption.Id)
            .HasColumnName("SaleRedemptionId")
            .ValueGeneratedOnAdd();
        builder.Property(redemption => redemption.CampaignNameSnapshot)
            .HasMaxLength(150)
            .IsRequired();
        builder.Property(redemption => redemption.BadgeTextSnapshot)
            .HasMaxLength(80);
        builder.Property(redemption => redemption.EligibilityTypeSnapshot)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(redemption => redemption.OriginalPrice)
            .HasPrecision(18, 2);
        builder.Property(redemption => redemption.FinalPrice)
            .HasPrecision(18, 2);
        builder.Property(redemption => redemption.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(SaleRedemptionStatus.Reserved)
            .IsRequired();

        builder.HasIndex(redemption => redemption.PaymentId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(redemption => redemption.UserSubscriptionId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(redemption => new
        {
            redemption.SaleCampaignId,
            redemption.Status
        });
        builder.HasIndex(redemption => new
        {
            redemption.SaleCampaignId,
            redemption.UserId,
            redemption.Status
        });
        builder.HasIndex(redemption => redemption.SaleCampaignPlanId);
        builder.HasIndex(redemption => redemption.UserId);
        builder.HasIndex(redemption => new
            {
                redemption.UserId,
                redemption.EligibilityTypeSnapshot
            })
            .IsUnique()
            .HasDatabaseName("UX_SaleRedemption_FirstPurchase_User")
            .HasFilter(
                "\"IsDeleted\" = false AND \"EligibilityTypeSnapshot\" = 'FirstPurchase' AND \"Status\" IN ('Reserved', 'Completed')");

        builder.HasOne(redemption => redemption.SaleCampaign)
            .WithMany(campaign => campaign.Redemptions)
            .HasForeignKey(redemption => redemption.SaleCampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.SaleCampaignPlan)
            .WithMany(campaignPlan => campaignPlan.Redemptions)
            .HasForeignKey(redemption => redemption.SaleCampaignPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.Plan)
            .WithMany()
            .HasForeignKey(redemption => redemption.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.UserSubscription)
            .WithOne()
            .HasForeignKey<SaleRedemption>(redemption => redemption.UserSubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.Payment)
            .WithOne(payment => payment.SaleRedemption)
            .HasForeignKey<SaleRedemption>(redemption => redemption.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(redemption => redemption.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
