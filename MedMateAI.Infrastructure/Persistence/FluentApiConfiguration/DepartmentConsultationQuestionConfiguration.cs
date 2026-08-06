using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class DepartmentConsultationQuestionConfiguration
    : IEntityTypeConfiguration<DepartmentConsultationQuestion>
{
    public void Configure(EntityTypeBuilder<DepartmentConsultationQuestion> builder)
    {
        builder.ToTable("DepartmentConsultationQuestion");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("QuestionId").ValueGeneratedOnAdd();

        builder.Property(x => x.QuestionText)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Category)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.DepartmentId, x.Category, x.SortOrder });

        builder.HasIndex(x => new { x.DepartmentId, x.QuestionText })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(x => x.Department)
            .WithMany(x => x.DepartmentConsultationQuestions)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
