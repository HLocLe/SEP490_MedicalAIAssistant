using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabIndicatorAliasConfiguration : IEntityTypeConfiguration<LabIndicatorAlias>
{
    public void Configure(EntityTypeBuilder<LabIndicatorAlias> builder)
    {
        builder.ToTable("LabIndicatorAlias");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("AliasId").ValueGeneratedOnAdd();

        builder.Property(x => x.AliasText).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10);

        builder.HasIndex(x => x.AliasText);
        builder.HasIndex(x => new { x.IndicatorId, x.AliasText })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
