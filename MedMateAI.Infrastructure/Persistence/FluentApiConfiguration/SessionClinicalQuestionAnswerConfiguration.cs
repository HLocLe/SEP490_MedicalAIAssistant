using System.Text.Json;
using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class SessionClinicalQuestionAnswerConfiguration : IEntityTypeConfiguration<SessionClinicalQuestionAnswer>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<SessionClinicalQuestionAnswer> builder)
    {
        builder.ToTable("SessionClinicalQuestionAnswer");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("SessionClinicalQuestionAnswerId");

        var answerValuesComparer = new ValueComparer<Dictionary<string, bool>>(
            (left, right) => JsonSerializer.Serialize(left, JsonOptions)
                == JsonSerializer.Serialize(right, JsonOptions),
            dictionary => dictionary.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            dictionary => dictionary.ToDictionary(pair => pair.Key, pair => pair.Value));

        builder.Property(x => x.AnswerValues)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                dictionary => JsonSerializer.Serialize(dictionary, JsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, bool>>(json, JsonOptions)
                    ?? new Dictionary<string, bool>())
            .Metadata.SetValueComparer(answerValuesComparer);

        builder.HasIndex(x => new { x.SymptomAnalysisSessionId, x.ClinicalQuestionId })
            .IsUnique();

        builder.HasOne(x => x.SymptomAnalysisSession)
            .WithMany(x => x.ClinicalQuestionAnswers)
            .HasForeignKey(x => x.SymptomAnalysisSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ClinicalQuestion)
            .WithMany(x => x.SessionAnswers)
            .HasForeignKey(x => x.ClinicalQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
