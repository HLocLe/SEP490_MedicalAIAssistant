using System.Text.Json;
using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class ClinicalQuestionConfiguration : IEntityTypeConfiguration<ClinicalQuestion>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<ClinicalQuestion> builder)
    {
        builder.ToTable("ClinicalQuestions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("QuestionId").ValueGeneratedOnAdd();

        builder.Property(x => x.ChapterId);

        builder.Property(x => x.ChapterCode)
            .HasMaxLength(10);

        builder.Property(x => x.QuestionVi)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.EnglishPrefix)
            .HasMaxLength(500);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        var answersComparer = new ValueComparer<Dictionary<string, string>>(
            (left, right) => JsonSerializer.Serialize(left, JsonOptions)
                == JsonSerializer.Serialize(right, JsonOptions),
            dictionary => dictionary.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            dictionary => dictionary.ToDictionary(pair => pair.Key, pair => pair.Value));

        builder.Property(x => x.Answers)
            .HasColumnType("jsonb")
            .HasConversion(
                dictionary => JsonSerializer.Serialize(dictionary, JsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                    ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(answersComparer);

        builder.HasIndex(x => new { x.ChapterId, x.SortOrder });

        builder.HasOne(x => x.IcdChapter)
            .WithMany(x => x.ClinicalQuestions)
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
