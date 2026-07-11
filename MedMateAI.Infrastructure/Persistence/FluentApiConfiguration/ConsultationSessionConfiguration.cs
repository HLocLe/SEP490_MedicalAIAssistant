using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class ConsultationSessionConfiguration : IEntityTypeConfiguration<ConsultationSession>
{
    public void Configure(EntityTypeBuilder<ConsultationSession> builder)
    {
        builder.ToTable("ConsultationSession");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ConsultationSessionId").ValueGeneratedOnAdd();

        builder.Property(x => x.UserSymptoms)
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(ConsultationSessionStatus.Processing);

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.ConsultationSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Department)
            .WithMany(x => x.ConsultationSessions)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ConsultationQuestions)
            .WithOne(x => x.ConsultationSession)
            .HasForeignKey(x => x.ConsultationSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
