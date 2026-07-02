using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctor");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("DoctorId").ValueGeneratedOnAdd();

        builder.Property(x => x.UserId)
            .IsRequired(false);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithOne(x => x.Doctor)
            .HasForeignKey<Doctor>(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.DepartmentRole)
            .HasConversion<int>()
            .HasDefaultValue(DepartmentRole.Doctor)
            .IsRequired();

        builder.HasOne(x => x.FacilityDepartment)
            .WithMany(x => x.Doctors)
            .HasForeignKey(x => x.FacilityDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
