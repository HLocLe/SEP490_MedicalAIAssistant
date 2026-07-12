using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class MedicalFacilityConfiguration : IEntityTypeConfiguration<MedicalFacility>
{
    public void Configure(EntityTypeBuilder<MedicalFacility> builder)
    {
        builder.ToTable("MedicalFacility");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FacilityId").ValueGeneratedOnAdd();

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(x => x.FacilityType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(MedicalFacilityType.Hospital)
            .IsRequired();
    }
}
