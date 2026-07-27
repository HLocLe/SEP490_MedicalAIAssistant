using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanFoodSourceConfiguration : IEntityTypeConfiguration<RecoveryPlanFoodSource>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanFoodSource> builder)
    {
        builder.ToTable("RecoveryPlanFoodSource");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanFoodSourceId").ValueGeneratedOnAdd();
        builder.Property(x => x.FoodName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SuggestedServing).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => x.RecoveryPlanNutrientTargetId);
        builder.HasIndex(x => new { x.RecoveryPlanNutrientTargetId, x.SortOrder }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasOne(x => x.RecoveryPlanNutrientTarget).WithMany(x => x.FoodSources).HasForeignKey(x => x.RecoveryPlanNutrientTargetId).OnDelete(DeleteBehavior.Cascade);
    }
}
