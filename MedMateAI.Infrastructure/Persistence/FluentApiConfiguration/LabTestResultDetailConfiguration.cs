using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabTestResultDetailConfiguration : IEntityTypeConfiguration<LabTestResultDetail>
{
    public void Configure(EntityTypeBuilder<LabTestResultDetail> builder)
    {
        builder.ToTable("LabTestResultDetail");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ResultDetailId").ValueGeneratedOnAdd();

        builder.Property(x => x.RawExtractedName).HasMaxLength(255);
        builder.Property(x => x.RawExtractedValue).HasMaxLength(100);
        builder.Property(x => x.ReferenceUnitUsed).HasMaxLength(50);

        builder.HasOne(x => x.AdviceCache)
            .WithMany(x => x.LabTestResultDetails)
            .HasForeignKey(x => x.AdviceCacheId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
