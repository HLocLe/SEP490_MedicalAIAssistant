using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabIndicatorMasterConfiguration : IEntityTypeConfiguration<LabIndicatorMaster>
{
    public void Configure(EntityTypeBuilder<LabIndicatorMaster> builder)
    {
        builder.ToTable("LabIndicatorMaster");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("IndicatorId").ValueGeneratedOnAdd();

        builder.Property(x => x.Symbol).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Symbol).IsUnique();

        builder.Property(x => x.FullName).HasMaxLength(255);
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasMany(x => x.LabIndicatorAliases)
            .WithOne(x => x.Indicator)
            .HasForeignKey(x => x.IndicatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.LabIndicatorReferenceRanges)
            .WithOne(x => x.Indicator)
            .HasForeignKey(x => x.IndicatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.LabTestResultDetails)
            .WithOne(x => x.Indicator)
            .HasForeignKey(x => x.IndicatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LabIndicatorAdviceCaches)
            .WithOne(x => x.Indicator)
            .HasForeignKey(x => x.IndicatorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
