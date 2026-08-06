using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanPhaseConfiguration : IEntityTypeConfiguration<RecoveryPlanPhase>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanPhase> builder)
    {
        builder.ToTable("RecoveryPlanPhase", t =>
        {
            t.HasCheckConstraint("CK_RecoveryPlanPhase_Days", "\"StartDay\" >= 1 AND \"EndDay\" >= \"StartDay\"");
            t.HasCheckConstraint("CK_RecoveryPlanPhase_SleepAndRest", "\"SleepAndRestHoursPerDay\" IS NULL OR (\"SleepAndRestHoursPerDay\" >= 0 AND \"SleepAndRestHoursPerDay\" <= 24)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanPhaseId").ValueGeneratedOnAdd();
        builder.Property(x => x.PhaseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SleepAndRestHoursPerDay).HasPrecision(4, 2);
        builder.Property(x => x.Instruction).HasMaxLength(2000);
        builder.HasIndex(x => x.RecoveryPlanId);
        builder.HasIndex(x => new { x.RecoveryPlanId, x.SortOrder }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasOne(x => x.RecoveryPlan).WithMany(x => x.Phases).HasForeignKey(x => x.RecoveryPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}
