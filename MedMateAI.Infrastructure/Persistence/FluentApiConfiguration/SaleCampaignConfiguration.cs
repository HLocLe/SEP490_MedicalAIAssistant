using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class SaleCampaignConfiguration : IEntityTypeConfiguration<SaleCampaign>
{
    public void Configure(EntityTypeBuilder<SaleCampaign> builder)
    {
        builder.ToTable("SaleCampaign", table =>
        {
            table.HasCheckConstraint(
                "CK_SaleCampaign_Window",
                "\"EndAt\" > \"StartAt\"");
            table.HasCheckConstraint(
                "CK_SaleCampaign_MaxRedemptions",
                "\"MaxRedemptions\" IS NULL OR \"MaxRedemptions\" >= 1");
            table.HasCheckConstraint(
                "CK_SaleCampaign_MaxRedemptionsPerUser",
                "\"MaxRedemptionsPerUser\" IS NULL OR \"MaxRedemptionsPerUser\" >= 1");
            table.HasCheckConstraint(
                "CK_SaleCampaign_Limits",
                "\"MaxRedemptions\" IS NULL OR \"MaxRedemptionsPerUser\" IS NULL OR \"MaxRedemptionsPerUser\" <= \"MaxRedemptions\"");
            table.HasCheckConstraint(
                "CK_SaleCampaign_Priority",
                "\"Priority\" >= 0 AND \"Priority\" <= 1000");
        });

        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.Id)
            .HasColumnName("SaleCampaignId")
            .ValueGeneratedOnAdd();
        builder.Property(campaign => campaign.Name)
            .HasMaxLength(150)
            .IsRequired();
        builder.Property(campaign => campaign.Description)
            .HasMaxLength(1000);
        builder.Property(campaign => campaign.BadgeText)
            .HasMaxLength(80);
        builder.Property(campaign => campaign.EligibilityType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(SaleCampaignEligibilityType.All)
            .IsRequired();
        builder.Property(campaign => campaign.AnnounceToUsers)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(campaign => new
        {
            campaign.IsActive,
            campaign.StartAt,
            campaign.EndAt,
            campaign.Priority
        });
    }
}
