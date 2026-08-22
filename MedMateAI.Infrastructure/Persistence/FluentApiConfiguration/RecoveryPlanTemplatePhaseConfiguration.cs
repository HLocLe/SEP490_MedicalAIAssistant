using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanTemplatePhaseConfiguration
    : IEntityTypeConfiguration<RecoveryPlanTemplatePhase>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanTemplatePhase> builder)
    {
        builder.ToTable("RecoveryPlanTemplatePhase", table =>
        {
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplatePhase_Days",
                "\"StartDay\" >= 1 AND \"EndDay\" >= \"StartDay\"");
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplatePhase_SleepAndRest",
                "\"SleepAndRestHoursPerDay\" IS NULL OR (\"SleepAndRestHoursPerDay\" >= 0 AND \"SleepAndRestHoursPerDay\" <= 24)");
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplatePhase_SortOrder",
                "\"SortOrder\" >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("RecoveryPlanTemplatePhaseId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.PhaseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SleepAndRestHoursPerDay).HasPrecision(4, 2);
        builder.Property(x => x.Instruction).HasMaxLength(2000);

        builder.HasIndex(x => x.RecoveryPlanTemplateId);
        builder.HasIndex(x => new { x.RecoveryPlanTemplateId, x.SortOrder })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(x => x.RecoveryPlanTemplate)
            .WithMany(x => x.Phases)
            .HasForeignKey(x => x.RecoveryPlanTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
