using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class ConsultationQuestionConfiguration : IEntityTypeConfiguration<ConsultationQuestion>
{
    public void Configure(EntityTypeBuilder<ConsultationQuestion> builder)
    {
        builder.ToTable("ConsultationQuestion");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("QuestionId").ValueGeneratedOnAdd();

        builder.Property(x => x.Category)
            .HasMaxLength(64);

        builder.Property(x => x.Priority)
            .HasDefaultValue(0);
    }
}
