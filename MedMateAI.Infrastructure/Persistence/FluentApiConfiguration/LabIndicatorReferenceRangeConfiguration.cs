using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabIndicatorReferenceRangeConfiguration : IEntityTypeConfiguration<LabIndicatorReferenceRange>
{
    public void Configure(EntityTypeBuilder<LabIndicatorReferenceRange> builder)
    {
        builder.ToTable("LabIndicatorReferenceRange");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ReferenceRangeId").ValueGeneratedOnAdd();

        builder.Property(x => x.Unit).HasMaxLength(50);

        builder.HasIndex(x => new { x.IndicatorId, x.Gender, x.AgeGroup });
    }
}
