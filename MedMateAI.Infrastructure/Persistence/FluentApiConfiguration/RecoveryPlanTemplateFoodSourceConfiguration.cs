using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanTemplateFoodSourceConfiguration
    : IEntityTypeConfiguration<RecoveryPlanTemplateFoodSource>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanTemplateFoodSource> builder)
    {
        builder.ToTable("RecoveryPlanTemplateFoodSource", table =>
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplateFoodSource_SortOrder",
                "\"SortOrder\" >= 0"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("RecoveryPlanTemplateFoodSourceId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.FoodName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SuggestedServing).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => x.RecoveryPlanTemplateNutrientTargetId);
        builder.HasIndex(x => new
            {
                x.RecoveryPlanTemplateNutrientTargetId,
                x.SortOrder
            })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(x => x.RecoveryPlanTemplateNutrientTarget)
            .WithMany(x => x.FoodSources)
            .HasForeignKey(x => x.RecoveryPlanTemplateNutrientTargetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
