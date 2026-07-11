using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class PatientChronicDiseaseConfiguration : IEntityTypeConfiguration<PatientChronicDisease>
{
    public void Configure(EntityTypeBuilder<PatientChronicDisease> builder)
    {
        builder.ToTable("PatientChronicDisease");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PatientChronicDiseaseId").ValueGeneratedOnAdd();

        builder.Property(x => x.DiseaseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.From)
            .HasColumnName("FromDate")
            .HasColumnType("date");

        builder.Property(x => x.To)
            .HasColumnName("ToDate")
            .HasColumnType("date");
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => x.PatientProfileId);

        builder.HasOne(x => x.PatientProfile)
            .WithMany(x => x.ChronicDiseases)
            .HasForeignKey(x => x.PatientProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
