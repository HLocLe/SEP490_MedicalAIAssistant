using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabTestOcrExtractConfiguration : IEntityTypeConfiguration<LabTestOcrExtract>
{
    public void Configure(EntityTypeBuilder<LabTestOcrExtract> builder)
    {
        builder.ToTable("LabTestOcrExtract");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("OcrExtractId").ValueGeneratedOnAdd();

        builder.Property(x => x.ExtractedTestName).HasMaxLength(255);
        builder.Property(x => x.ExtractedValue).HasMaxLength(100);
        builder.Property(x => x.ExtractedUnit).HasMaxLength(50);
        builder.Property(x => x.ExtractedReferenceText).HasMaxLength(255);

        builder.HasIndex(x => new { x.TestSessionId, x.RowIndex });
    }
}
