using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class TreatmentJourneyConfiguration : IEntityTypeConfiguration<TreatmentJourney>
{
    public void Configure(EntityTypeBuilder<TreatmentJourney> builder)
    {
        builder.ToTable("TreatmentJourney");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TreatmentJourneyId").ValueGeneratedOnAdd();

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.TreatmentJourneys)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Facility)
            .WithMany(x => x.TreatmentJourneys)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Department)
            .WithMany(x => x.TreatmentJourneys)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.ApprovalStatus).HasMaxLength(50);

        builder.HasOne(x => x.Doctor)
            .WithMany(x => x.TreatmentJourneys)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);

      

        builder.HasMany(x => x.AIAnalyses)
            .WithOne(x => x.TreatmentJourney)
            .HasForeignKey(x => x.TreatmentJourneyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.UserMedications)
            .WithOne(x => x.TreatmentJourney)
            .HasForeignKey(x => x.TreatmentJourneyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
