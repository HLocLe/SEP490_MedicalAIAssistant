using System.Text.Json;
using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class FeedbackReviewConfiguration : IEntityTypeConfiguration<FeedbackReview>
{
    private static readonly JsonSerializerOptions ImageUrlsJsonOptions = new();

    public void Configure(EntityTypeBuilder<FeedbackReview> builder)
    {
        builder.ToTable("FeedbackReview");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FeedbackId").ValueGeneratedOnAdd();

        var imageUrlsComparer = new ValueComparer<Dictionary<string, string>>(
            (left, right) => JsonSerializer.Serialize(left, ImageUrlsJsonOptions)
                == JsonSerializer.Serialize(right, ImageUrlsJsonOptions),
            dictionary => dictionary.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            dictionary => dictionary.ToDictionary(pair => pair.Key, pair => pair.Value));

        builder.Property(x => x.ImageUrls)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                dictionary => JsonSerializer.Serialize(dictionary, ImageUrlsJsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, ImageUrlsJsonOptions)
                    ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(imageUrlsComparer);

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.FeedbackReviews)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Facility)
            .WithMany(x => x.FeedbackReviews)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
