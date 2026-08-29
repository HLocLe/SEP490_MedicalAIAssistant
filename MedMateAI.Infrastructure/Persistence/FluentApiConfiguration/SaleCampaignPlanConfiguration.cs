using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class SaleCampaignPlanConfiguration : IEntityTypeConfiguration<SaleCampaignPlan>
{
    public void Configure(EntityTypeBuilder<SaleCampaignPlan> builder)
    {
        builder.ToTable("SaleCampaignPlan", table =>
        {
            table.HasCheckConstraint(
                "CK_SaleCampaignPlan_SalePrice",
                "\"SalePrice\" IS NULL OR \"SalePrice\" > 0");
            table.HasCheckConstraint(
                "CK_SaleCampaignPlan_BonusCredit",
                "\"BonusCredit\" >= 0");
            table.HasCheckConstraint(
                "CK_SaleCampaignPlan_Benefit",
                "\"SalePrice\" IS NOT NULL OR \"BonusCredit\" > 0");
        });

        builder.HasKey(campaignPlan => campaignPlan.Id);
        builder.Property(campaignPlan => campaignPlan.Id)
            .HasColumnName("SaleCampaignPlanId")
            .ValueGeneratedOnAdd();
        builder.Property(campaignPlan => campaignPlan.SalePrice)
            .HasPrecision(18, 2);

        builder.HasIndex(campaignPlan => new
            {
                campaignPlan.SaleCampaignId,
                campaignPlan.PlanId
            })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(campaignPlan => new
        {
            campaignPlan.PlanId,
            campaignPlan.IsActive
        });

        builder.HasOne(campaignPlan => campaignPlan.SaleCampaign)
            .WithMany(campaign => campaign.CampaignPlans)
            .HasForeignKey(campaignPlan => campaignPlan.SaleCampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(campaignPlan => campaignPlan.Plan)
            .WithMany(plan => plan.SaleCampaignPlans)
            .HasForeignKey(campaignPlan => campaignPlan.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
