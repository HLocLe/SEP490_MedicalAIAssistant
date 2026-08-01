using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class LabTestSessionConfiguration : IEntityTypeConfiguration<LabTestSession>
{
    public void Configure(EntityTypeBuilder<LabTestSession> builder)
    {
        builder.ToTable("LabTestSession");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TestSessionId").ValueGeneratedOnAdd();

        builder.Property(x => x.DocumentUrl).HasMaxLength(2048);
        builder.Property(x => x.FacilityName).HasMaxLength(255);

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.LabTestSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LabTestOcrExtracts)
            .WithOne(x => x.TestSession)
            .HasForeignKey(x => x.TestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.LabTestResultDetails)
            .WithOne(x => x.TestSession)
            .HasForeignKey(x => x.TestSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
