using System.Text.Json;
using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class IcdChapterConfiguration : IEntityTypeConfiguration<IcdChapter>
{
    private static readonly JsonSerializerOptions KeywordWeightsJsonOptions = new();

    public void Configure(EntityTypeBuilder<IcdChapter> builder)
    {
        builder.ToTable("IcdChapters");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("IcdChapterId").ValueGeneratedOnAdd();

        builder.Property(x => x.ChapterCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(x => x.ChapterCode).IsUnique();

        builder.Property(x => x.ChapterName)
            .HasMaxLength(500)
            .IsRequired();

        var keywordWeightsComparer = new ValueComparer<Dictionary<string, int>>(
            (left, right) => JsonSerializer.Serialize(left, KeywordWeightsJsonOptions)
                == JsonSerializer.Serialize(right, KeywordWeightsJsonOptions),
            dictionary => dictionary.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            dictionary => dictionary.ToDictionary(pair => pair.Key, pair => pair.Value));

        builder.Property(x => x.KeywordWeights)
            .HasColumnType("jsonb")
            .HasConversion(
                dictionary => JsonSerializer.Serialize(dictionary, KeywordWeightsJsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, int>>(json, KeywordWeightsJsonOptions)
                    ?? new Dictionary<string, int>())
            .Metadata.SetValueComparer(keywordWeightsComparer);

        builder.HasMany(x => x.ClinicalQuestions)
            .WithOne(x => x.IcdChapter)
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.MedicalDepartments)
            .WithOne(x => x.IcdChapter)
            .HasForeignKey(x => x.ChapterCode)
            .HasPrincipalKey(x => x.ChapterCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
