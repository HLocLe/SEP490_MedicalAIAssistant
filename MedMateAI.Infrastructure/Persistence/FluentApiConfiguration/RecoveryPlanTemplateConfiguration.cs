using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanTemplateConfiguration
    : IEntityTypeConfiguration<RecoveryPlanTemplate>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanTemplate> builder)
    {
        builder.ToTable("RecoveryPlanTemplate", table =>
            table.HasCheckConstraint(
                "CK_RecoveryPlanTemplate_DurationDays",
                "\"DurationDays\" >= 1 AND \"DurationDays\" <= 365"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("RecoveryPlanTemplateId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.DiseaseGroup)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.TemplateName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PlanName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.Property(x => x.RecheckInstruction).HasMaxLength(2000);

        builder.HasIndex(x => x.DoctorId);
        builder.HasIndex(x => new { x.DoctorId, x.DiseaseGroup });

        builder.HasOne(x => x.Doctor)
            .WithMany(x => x.RecoveryPlanTemplates)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
