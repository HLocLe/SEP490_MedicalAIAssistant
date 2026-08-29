using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class DiseasePriorProbabilityConfiguration : IEntityTypeConfiguration<DiseasePriorProbability>
{
    public void Configure(EntityTypeBuilder<DiseasePriorProbability> builder)
    {
        builder.ToTable("DiseasePriorProbabilities");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("DiseasePriorProbabilityId").ValueGeneratedOnAdd();

        builder.Property(x => x.Icd10Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.Icd10Code)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.Property(x => x.DiseaseName)
            .HasMaxLength(500);

        builder.Property(x => x.PA)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);
    }
}
