using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class SymptomAnalysisSessionConfiguration : IEntityTypeConfiguration<SymptomAnalysisSession>
{
    public void Configure(EntityTypeBuilder<SymptomAnalysisSession> builder)
    {
        builder.ToTable("SymptomAnalysisSession");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("SymptomAnalysisSessionId").ValueGeneratedOnAdd();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(SymptomAnalysisSessionStatus.Processing);

        builder.Property(x => x.SessionType)
            .IsRequired()
            .HasDefaultValue(SymptomAnalysisSessionType.None);

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.SymptomAnalysisSessions)
            .HasForeignKey(x => x.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ChapterCode)
            .HasMaxLength(10);

        builder.HasOne(x => x.IcdChapter)
            .WithMany(x => x.SymptomAnalysisSessions)
            .HasForeignKey(x => x.ChapterCode)
            .HasPrincipalKey(x => x.ChapterCode)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ChapterCode);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.SessionType });

        builder.HasMany(x => x.SessionSymptoms)
            .WithOne(x => x.SymptomAnalysisSession)
            .HasForeignKey(x => x.SymptomAnalysisSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.DepartmentRecommendations)
            .WithOne(x => x.SymptomAnalysisSession)
            .HasForeignKey(x => x.SymptomAnalysisSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
