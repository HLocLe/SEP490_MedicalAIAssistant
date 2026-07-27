using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanNutrientTargetConfiguration : IEntityTypeConfiguration<RecoveryPlanNutrientTarget>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanNutrientTarget> builder)
    {
        builder.ToTable("RecoveryPlanNutrientTarget", t => t.HasCheckConstraint("CK_RecoveryPlanNutrientTarget_Amount", "\"AmountPerDay\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanNutrientTargetId").ValueGeneratedOnAdd();
        builder.Property(x => x.NutrientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AmountPerDay).HasPrecision(10, 2);
        builder.Property(x => x.Unit).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Instruction).HasMaxLength(1000);
        builder.HasIndex(x => x.RecoveryPlanPhaseId);
        builder.HasIndex(x => new { x.RecoveryPlanPhaseId, x.SortOrder }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasOne(x => x.RecoveryPlanPhase).WithMany(x => x.NutrientTargets).HasForeignKey(x => x.RecoveryPlanPhaseId).OnDelete(DeleteBehavior.Cascade);
    }
}
