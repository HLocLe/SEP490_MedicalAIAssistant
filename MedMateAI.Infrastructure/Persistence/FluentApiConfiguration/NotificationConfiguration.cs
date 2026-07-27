using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("NotificationId").ValueGeneratedOnAdd();
        builder.Property(x => x.NotificationType).HasMaxLength(100).HasDefaultValue("FOLLOW_UP_REMINDER").IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(256);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.Channel).HasMaxLength(32);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.AttemptCount).HasDefaultValue(0);
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.DedupeKey).HasMaxLength(256);
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.HasIndex(x => x.DedupeKey).IsUnique().HasFilter("\"DedupeKey\" IS NOT NULL");

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reminder).WithMany(x => x.Notifications).HasForeignKey(x => x.ReminderId).OnDelete(DeleteBehavior.SetNull);
    }
}
