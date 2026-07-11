using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class PatientProfileConfiguration : IEntityTypeConfiguration<PatientProfile>
{
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder.ToTable("PatientProfile");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PatientProfileId").ValueGeneratedOnAdd();

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithOne(x => x.PatientProfile)
            .HasForeignKey<PatientProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ChronicDiseases)
            .WithOne(x => x.PatientProfile)
            .HasForeignKey(x => x.PatientProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
