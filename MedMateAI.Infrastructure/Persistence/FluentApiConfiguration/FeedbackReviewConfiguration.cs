using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class FeedbackReviewConfiguration : IEntityTypeConfiguration<FeedbackReview>
{
    public void Configure(EntityTypeBuilder<FeedbackReview> builder)
    {
        builder.ToTable("FeedbackReview");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FeedbackId").ValueGeneratedOnAdd();

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

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
