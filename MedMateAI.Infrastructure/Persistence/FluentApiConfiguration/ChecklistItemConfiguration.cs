using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("ChecklistItem");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ChecklistItemId").ValueGeneratedOnAdd();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.IsMandatory)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.DepartmentId);
        builder.HasIndex(x => x.FacilityId);

        builder.HasOne(x => x.Department)
            .WithMany(x => x.ChecklistItems)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Facility)
            .WithMany(x => x.ChecklistItems)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
