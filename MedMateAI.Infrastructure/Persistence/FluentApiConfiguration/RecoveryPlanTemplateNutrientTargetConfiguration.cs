using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanTemplateNutrientTargetConfiguration
    : IEntityTypeConfiguration<RecoveryPlanTemplateNutrientTarget>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanTemplateNutrientTarget> builder)
    {
        builder.ToTable("RecoveryPlanTemplateNutrientTarget", table =>
        {
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplateNutrientTarget_Amount",
                "\"AmountPerDay\" > 0");
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplateNutrientTarget_SortOrder",
                "\"SortOrder\" >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("RecoveryPlanTemplateNutrientTargetId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.NutrientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AmountPerDay).HasPrecision(10, 2);
        builder.Property(x => x.Unit).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Instruction).HasMaxLength(1000);

        builder.HasIndex(x => x.RecoveryPlanTemplatePhaseId);
        builder.HasIndex(x => new { x.RecoveryPlanTemplatePhaseId, x.SortOrder })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(x => x.RecoveryPlanTemplatePhase)
            .WithMany(x => x.NutrientTargets)
            .HasForeignKey(x => x.RecoveryPlanTemplatePhaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
