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
            t.HasCheckConstraint("CK_RecoveryPlanPhase_Sleep", "\"SleepHoursPerDay\" IS NULL OR (\"SleepHoursPerDay\" >= 0 AND \"SleepHoursPerDay\" <= 24)");
            t.HasCheckConstraint("CK_RecoveryPlanPhase_Rest", "\"RestHoursPerDay\" IS NULL OR (\"RestHoursPerDay\" >= 0 AND \"RestHoursPerDay\" <= 24)");
            t.HasCheckConstraint("CK_RecoveryPlanPhase_TotalHours", "\"SleepHoursPerDay\" IS NULL OR \"RestHoursPerDay\" IS NULL OR \"SleepHoursPerDay\" + \"RestHoursPerDay\" <= 24");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanPhaseId").ValueGeneratedOnAdd();
        builder.Property(x => x.PhaseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SleepHoursPerDay).HasPrecision(4, 2);
        builder.Property(x => x.RestHoursPerDay).HasPrecision(4, 2);
        builder.Property(x => x.Instruction).HasMaxLength(2000);
        builder.HasIndex(x => x.RecoveryPlanId);
        builder.HasIndex(x => new { x.RecoveryPlanId, x.SortOrder }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasOne(x => x.RecoveryPlan).WithMany(x => x.Phases).HasForeignKey(x => x.RecoveryPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}
